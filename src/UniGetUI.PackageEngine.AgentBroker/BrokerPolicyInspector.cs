using System.Text;
using System.Text.RegularExpressions;
using Devolutions.Now.Policy.Api;
using Devolutions.Now.Policy.Client;
using Devolutions.Now.Policy.Model;
using UniGetUI.Core.Logging;
using ApiElevation = Devolutions.Now.Policy.Api.Elevation;

namespace UniGetUI.PackageEngine.AgentBroker;

public enum BrokerPolicyInspectionStatus
{
    Connected,
    AgentUnavailable,
    Unsupported,
    AccessDenied,
    PolicyUnavailable,
    InvalidResponse,
    UnsupportedPlatform,
}

public sealed record BrokerPolicyInspectionResult(
    BrokerPolicyInspectionStatus Status,
    PolicyResponse? Response = null,
    string? CanonicalJson = null,
    string? ErrorMessage = null);

public interface IBrokerPolicyInspector
{
    Task<BrokerPolicyInspectionResult> InspectAsync(CancellationToken cancellationToken);
}

public sealed partial class BrokerPolicyInspector : IBrokerPolicyInspector
{
    private readonly Func<BrokerClient> _clientFactory;
    private readonly Func<bool> _isWindows;

    public BrokerPolicyInspector()
        : this(
            CreateStandardClient,
            OperatingSystem.IsWindows)
    {
    }

    private static BrokerClient CreateStandardClient() =>
        BrokerClientFactory.Create(ApiElevation.Standard);

    public BrokerPolicyInspector(Func<BrokerClient> clientFactory, Func<bool>? isWindows = null)
    {
        _clientFactory = clientFactory;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
    }

    public async Task<BrokerPolicyInspectionResult> InspectAsync(CancellationToken cancellationToken)
    {
        if (!_isWindows())
        {
            return new(BrokerPolicyInspectionStatus.UnsupportedPlatform);
        }

        try
        {
            using BrokerClient client = _clientFactory();
            PolicyResponse response = await client.GetPolicy(cancellationToken).ConfigureAwait(false);
            if (!HasRequiredData(response))
            {
                Logger.Warn("[AgentBroker] Active policy response contained invalid required data.");
                return new(
                    BrokerPolicyInspectionStatus.InvalidResponse,
                    ErrorMessage: "The broker response contained invalid policy data.");
            }

            return new(
                BrokerPolicyInspectionStatus.Connected,
                response,
                PolicySerializer.Serialize(response.Policy));
        }
        catch (BrokerClientException ex)
        {
            Logger.Warn($"[AgentBroker] Active policy inspection failed: {ex}");
            return new(MapFailure(ex), ErrorMessage: ex.BrokerError?.Message ?? ex.Message);
        }
    }

    private static bool HasRequiredData(PolicyResponse response)
    {
        PolicyDocument? policy = response.Policy;
        if (response.ResponseKind != BrokerApi.PolicyResponseKind
            || !IsResponseVersion(response.ResponseVersion)
            || response.Server is null
            || !IsRequiredString(response.Server.ServerVersion, 128)
            || !Enum.IsDefined(response.Server.Transport)
            || !BrokerPolicyDocumentValidator.HasRequiredData(policy))
        {
            return false;
        }

        return true;
    }

    private static bool IsRequiredString(string? value, int maxLength)
    {
        if (value is null)
        {
            return false;
        }

        int length = value.EnumerateRunes().Take(maxLength + 1).Count();
        return length is > 0 && length <= maxLength;
    }

    private static bool IsResponseVersion(string? value)
    {
        return !string.IsNullOrEmpty(value)
            && ResponseVersionRegex().IsMatch(value);
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex ResponseVersionRegex();

    private static BrokerPolicyInspectionStatus MapFailure(BrokerClientException ex)
    {
        if (ex.StatusCode == 404 && ex.BrokerError is null)
        {
            return BrokerPolicyInspectionStatus.Unsupported;
        }

        if (BrokerPolicyFailure.IsAccessDenied(ex))
        {
            return BrokerPolicyInspectionStatus.AccessDenied;
        }

        return ex.Kind switch
        {
            _ when BrokerPolicyFailure.IsTransportUnavailable(ex) =>
                BrokerPolicyInspectionStatus.AgentUnavailable,
            BrokerClientErrorKind.EmptyResponse or BrokerClientErrorKind.InvalidResponse =>
                BrokerPolicyInspectionStatus.InvalidResponse,
            BrokerClientErrorKind.BrokerError when ex.BrokerError is not null =>
                BrokerPolicyInspectionStatus.PolicyUnavailable,
            _ => BrokerPolicyInspectionStatus.InvalidResponse,
        };
    }
}
