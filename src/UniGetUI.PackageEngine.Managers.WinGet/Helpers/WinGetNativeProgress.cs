using Microsoft.Management.Deployment;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using ComInstallOptions = Microsoft.Management.Deployment.InstallOptions;
using ComUninstallOptions = Microsoft.Management.Deployment.UninstallOptions;
using UniInstallOptions = UniGetUI.PackageEngine.Serializable.InstallOptions;

namespace UniGetUI.PackageEngine.Managers.WingetManager;

/// <summary>
/// Maps WinGet COM progress structs to the generic <see cref="OperationProgress"/>
/// model. The UI never sees these COM types.
/// Design notes from the WinGet IDL are honored: there is no reliable total
/// percent across download+install, byte counters only exist for downloads
/// performed by Windows Package Manager itself, and an unknown install phase
/// stays indeterminate instead of synthesizing a total.
/// </summary>
internal static class WinGetProgressMapper
{
    public static OperationProgress MapInstall(InstallProgress progress)
    {
        return progress.State switch
        {
            PackageInstallProgressState.Queued => OperationProgress.Queued,
            PackageInstallProgressState.Downloading => OperationProgress.FromDownload(
                progress.BytesDownloaded,
                progress.BytesRequired,
                progress.DownloadProgress
            ),
            PackageInstallProgressState.Installing => OperationProgress.FromInstall(
                progress.InstallationProgress
            ),
            PackageInstallProgressState.PostInstall => OperationProgress.Finalizing,
            PackageInstallProgressState.Finished => OperationProgress.Completed,
            _ => OperationProgress.Unknown,
        };
    }

    public static OperationProgress MapUninstall(UninstallProgress progress)
    {
        return progress.State switch
        {
            PackageUninstallProgressState.Queued => OperationProgress.Queued,
            PackageUninstallProgressState.Uninstalling => OperationProgress.FromUninstall(
                progress.UninstallationProgress
            ),
            PackageUninstallProgressState.PostUninstall => OperationProgress.Finalizing,
            PackageUninstallProgressState.Finished => OperationProgress.Completed,
            _ => OperationProgress.Unknown,
        };
    }
}

/// <summary>
/// Executes WinGet install/update/uninstall through the native COM API when it
/// can faithfully honor the requested options, reporting structured progress.
/// Returns null when the operation must fall back to the CLI path (COM
/// unavailable, custom CLI args, version lookup miss, elevation needed while
/// not elevated, ...). Never throws for fallback conditions; only unexpected
/// COM failures propagate as <see cref="OperationVeredict.Failure"/>.
/// </summary>
internal static class WinGetNativeOperationRunner
{
    public static bool CanUseNative(IPackage package, UniInstallOptions options, OperationType role)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(options);

        if (role is not (OperationType.Install or OperationType.Update or OperationType.Uninstall))
            return false;

        if (package.Source.IsVirtualManager)
            return false;

        if (NativeWinGetHelper.ExternalFactory is null || NativeWinGetHelper.ExternalWinGetManager is null)
            return false;

        if (WinGetHelper.Instance is not NativeWinGetHelper)
            return false;

        if (NativePackageHandler.GetPackage(package) is null)
            return false;

        // Custom CLI parameters have no COM equivalent; honor them via CLI.
        IReadOnlyList<string> customArgs = role switch
        {
            OperationType.Update => options.CustomParameters_Update,
            OperationType.Uninstall => options.CustomParameters_Uninstall,
            _ => options.CustomParameters_Install,
        };
        if (customArgs.Any(static arg => !string.IsNullOrWhiteSpace(arg)))
            return false;

        return true;
    }

    public static async Task<OperationVeredict?> ExecuteAsync(
        IPackage package,
        UniInstallOptions options,
        OperationType role,
        Action<OperationProgress> report,
        Action<string> logInfo,
        Action<string> logError,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(logInfo);
        ArgumentNullException.ThrowIfNull(logError);

        var factory = NativeWinGetHelper.ExternalFactory;
        var manager = NativeWinGetHelper.ExternalWinGetManager;
        var nativePackage = NativePackageHandler.GetPackage(package);
        if (factory is null || manager is null || nativePackage is null)
            return null;

        // Mirror the CLI path: detect elevation-requiring installers first so the
        // requested elevation matches regardless of where the operation runs.
        try
        {
            package.Manager.OperationHelper.ApplyElevationRequirements(package, options, role);
        }
        catch (UnauthorizedAccessException ex)
        {
            logError(ex.Message);
            return OperationVeredict.Failure;
        }
        catch (Exception ex)
        {
            Logger.Error("WinGet native progress: elevation detection failed, falling back to CLI");
            Logger.Error(ex);
            return null;
        }

        // The in-proc COM server runs as the current user; an operation that must
        // elevate while we are not elevated has to go through the elevator (CLI).
        bool requiresAdmin =
            package.OverridenOptions.RunAsAdministrator is true || options.RunAsAdministrator;
        if (requiresAdmin && !CoreTools.IsAdministrator())
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return role switch
            {
                OperationType.Uninstall => await ExecuteUninstallAsync(
                    package,
                    options,
                    nativePackage,
                    factory,
                    manager,
                    report,
                    logInfo,
                    logError,
                    cancellationToken
                ).ConfigureAwait(false),
                OperationType.Install or OperationType.Update => await ExecuteInstallOrUpgradeAsync(
                    package,
                    options,
                    role,
                    nativePackage,
                    factory,
                    manager,
                    report,
                    logInfo,
                    logError,
                    cancellationToken
                ).ConfigureAwait(false),
                _ => null,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OperationVeredict.Canceled;
        }
        catch (Exception ex)
        {
            Logger.Error("WinGet native operation failed, falling back to CLI when possible:");
            Logger.Error(ex);
            logError($"Native WinGet operation failed: {ex.Message}");
            return OperationVeredict.Failure;
        }
    }

    private static async Task<OperationVeredict?> ExecuteInstallOrUpgradeAsync(
        IPackage package,
        UniInstallOptions options,
        OperationType role,
        CatalogPackage nativePackage,
        WindowsPackageManager.Interop.WindowsPackageManagerFactory factory,
        PackageManager manager,
        Action<OperationProgress> report,
        Action<string> logInfo,
        Action<string> logError,
        CancellationToken cancellationToken
    )
    {
        ComInstallOptions comOptions = factory.CreateInstallOptions();
        if (!TryApplyInstallOptions(package, options, role, nativePackage, comOptions, logError))
            return null;

        bool isUpgrade = role is OperationType.Update;
        logInfo(
            isUpgrade
                ? $"Starting native WinGet upgrade for {package.Id}..."
                : $"Starting native WinGet install for {package.Id}..."
        );

        var asyncOp = isUpgrade
            ? manager.UpgradePackageAsync(nativePackage, comOptions)
            : manager.InstallPackageAsync(nativePackage, comOptions);
        asyncOp.Progress += (_, progress) =>
        {
            try
            {
                report(WinGetProgressMapper.MapInstall(progress));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        };
        using var cancelReg = cancellationToken.Register(static state =>
        {
            try
            {
                ((Windows.Foundation.IAsyncOperationWithProgress<InstallResult, InstallProgress>)state!).Cancel();
            }
            catch
            {
                // The operation may already be completed; cancellation is best-effort.
            }
        }, asyncOp);

        InstallResult result;
        try
        {
            result = await asyncOp.AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OperationVeredict.Canceled;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OperationVeredict.Canceled;
        }

        return InterpretInstallResult(package, role, result, logInfo, logError);
    }

    private static async Task<OperationVeredict?> ExecuteUninstallAsync(
        IPackage package,
        UniInstallOptions options,
        CatalogPackage nativePackage,
        WindowsPackageManager.Interop.WindowsPackageManagerFactory factory,
        PackageManager manager,
        Action<OperationProgress> report,
        Action<string> logInfo,
        Action<string> logError,
        CancellationToken cancellationToken
    )
    {
        ComUninstallOptions comOptions = factory.CreateUninstallOptions();
        comOptions.PackageUninstallMode = options.InteractiveInstallation
            ? PackageUninstallMode.Interactive
            : PackageUninstallMode.Silent;

        logInfo($"Starting native WinGet uninstall for {package.Id}...");

        var asyncOp = manager.UninstallPackageAsync(nativePackage, comOptions);
        asyncOp.Progress += (_, progress) =>
        {
            try
            {
                report(WinGetProgressMapper.MapUninstall(progress));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        };
        using var cancelReg = cancellationToken.Register(static state =>
        {
            try
            {
                ((Windows.Foundation.IAsyncOperationWithProgress<UninstallResult, UninstallProgress>)state!).Cancel();
            }
            catch
            {
                // Best-effort; the operation may already be completed.
            }
        }, asyncOp);

        UninstallResult result;
        try
        {
            result = await asyncOp.AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OperationVeredict.Canceled;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OperationVeredict.Canceled;
        }

        return InterpretUninstallResult(result, logInfo, logError);
    }

    internal static bool TryApplyInstallOptions(
        IPackage package,
        UniInstallOptions options,
        OperationType role,
        CatalogPackage nativePackage,
        ComInstallOptions comOptions,
        Action<string> logError
    )
    {
        // Scope
        string scope = package.OverridenOptions.Scope ?? options.InstallationScope;
        if (!package.OverridenOptions.WinGet_DropArchAndScope)
        {
            comOptions.PackageInstallScope = scope switch
            {
                PackageScope.User => PackageInstallScope.User,
                PackageScope.Machine => PackageInstallScope.System,
                _ => PackageInstallScope.Any,
            };
        }
        else
        {
            comOptions.PackageInstallScope = PackageInstallScope.Any;
        }

        // Mode
        comOptions.PackageInstallMode = options.InteractiveInstallation
            ? PackageInstallMode.Interactive
            : PackageInstallMode.Silent;

        // Hash / agreements / force mirror the CLI flags.
        comOptions.AllowHashMismatch = options.SkipHashCheck;
        comOptions.AcceptPackageAgreements = true;
        comOptions.Force = true;

        if (role is OperationType.Update)
            comOptions.AllowUpgradeToUnknownVersion = true;

        // Location
        string? location = role is OperationType.Update
            ? WinGetPkgOperationHelper.GetEffectiveUpdateLocation(package, options)
            : options.CustomInstallLocation;
        if (!string.IsNullOrWhiteSpace(location))
            comOptions.PreferredInstallLocation = location;

        // Architecture preference: single-entry list forces the requested arch,
        // matching CLI --architecture. Defaults are left untouched otherwise.
        if (!package.OverridenOptions.WinGet_DropArchAndScope)
        {
            Windows.System.ProcessorArchitecture? arch = MapArchitecture(options.Architecture);
            if (arch.HasValue)
            {
                try
                {
                    comOptions.AllowedArchitectures.Clear();
                    comOptions.AllowedArchitectures.Add(arch.Value);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Could not set WinGet COM architecture preference: {ex.Message}");
                }
            }
        }

        // Version pin: PackageVersionId instances are lookup-only; resolve the
        // requested version from AvailableVersions, else fall back to CLI.
        if (role is OperationType.Install && !string.IsNullOrWhiteSpace(options.Version))
        {
            var match = FindPackageVersionId(nativePackage, options.Version);
            if (match is null)
            {
                logError(
                    $"Requested version {options.Version} was not found for {package.Id}; falling back to CLI."
                );
                return false;
            }
            comOptions.PackageVersionId = match;
        }

        return true;
    }

    private static Windows.System.ProcessorArchitecture? MapArchitecture(string architecture) =>
        architecture switch
        {
            Architecture.x86 => Windows.System.ProcessorArchitecture.X86,
            Architecture.x64 => Windows.System.ProcessorArchitecture.X64,
            Architecture.arm64 => Windows.System.ProcessorArchitecture.Arm64,
            Architecture.arm32 => Windows.System.ProcessorArchitecture.Arm,
            _ => null,
        };

    private static PackageVersionId? FindPackageVersionId(CatalogPackage nativePackage, string version)
    {
        try
        {
            var available = nativePackage.AvailableVersions;
            if (available is null)
                return null;
            foreach (var candidate in NativeWinGetCollection.Copy(available))
            {
                if (
                    string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase)
                )
                    return candidate;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not enumerate WinGet versions for {nativePackage.Id}: {ex.Message}");
        }
        return null;
    }

    private static OperationVeredict InterpretInstallResult(
        IPackage package,
        OperationType role,
        InstallResult result,
        Action<string> logInfo,
        Action<string> logError
    )
    {
        logInfo($"Native WinGet result: {result.Status} (0x{result.ExtendedErrorCode:X8})");
        if (result.RebootRequired)
            logInfo("A restart is required to finish the installation.");

        switch (result.Status)
        {
            case InstallResultStatus.Ok:
                WinGetPkgOperationHelper.MarkUpgradeAsDoneForNative(package, role);
                return OperationVeredict.Success;
            case InstallResultStatus.NoApplicableUpgrade:
                // Already at the requested version; report success like the CLI
                // "already installed" path instead of failing the operation.
                WinGetPkgOperationHelper.MarkUpgradeAsDoneForNative(package, role);
                logInfo("The package is already at the requested version.");
                return OperationVeredict.Success;
            case InstallResultStatus.NoApplicableInstallers:
                if (
                    role is OperationType.Update
                    && !package.OverridenOptions.WinGet_DropArchAndScope
                )
                {
                    // Mirror the CLI retry: the forced scope/architecture may exclude
                    // the only installer; drop the constraints for the CLI fallback.
                    package.OverridenOptions.WinGet_DropArchAndScope = true;
                    logError(
                        "No applicable installer found with the current scope/architecture constraints."
                    );
                }
                else
                {
                    logError("No applicable installer was found for this system.");
                    if (role is OperationType.Update)
                        WinGetPkgOperationHelper.SuppressPhantomUpgrade(package);
                }
                return OperationVeredict.Failure;
            case InstallResultStatus.DownloadError:
            case InstallResultStatus.InstallError:
            case InstallResultStatus.ManifestError:
            case InstallResultStatus.CatalogError:
            case InstallResultStatus.InternalError:
            case InstallResultStatus.InvalidOptions:
            case InstallResultStatus.BlockedByPolicy:
            case InstallResultStatus.PackageAgreementsNotAccepted:
            default:
                if (
                    result.Status is InstallResultStatus.ManifestError
                        or InstallResultStatus.NoApplicableInstallers
                    && role is OperationType.Update
                )
                    WinGetPkgOperationHelper.SuppressPhantomUpgrade(package);
                logError($"Native WinGet operation failed: {result.Status} (0x{result.ExtendedErrorCode:X8})");
                return OperationVeredict.Failure;
        }
    }

    private static OperationVeredict InterpretUninstallResult(
        UninstallResult result,
        Action<string> logInfo,
        Action<string> logError
    )
    {
        logInfo($"Native WinGet uninstall result: {result.Status} (0x{result.ExtendedErrorCode:X8})");
        if (result.RebootRequired)
            logInfo("A restart is required to finish the uninstallation.");

        return result.Status switch
        {
            UninstallResultStatus.Ok => OperationVeredict.Success,
            _ => FailUninstall(result, logError),
        };
    }

    private static OperationVeredict FailUninstall(UninstallResult result, Action<string> logError)
    {
        logError($"Native WinGet uninstall failed: {result.Status} (0x{result.ExtendedErrorCode:X8})");
        return OperationVeredict.Failure;
    }
}
