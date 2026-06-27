using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Server-side authority bridge for /dispatch.
/// Client-supplied EndpointRequestDto.Context is never trusted for auth/capability keys;
/// reserved keys are stripped and overwritten from validated JWT claims before scheduling.
/// </summary>
public static class DispatchAuthContext
{
    public const string AuthenticatedUserIdKey = "authenticated_user_id";
    public const string AuthenticatedRolesKey = "authenticated_roles";
    public const string ResolvedCapabilitiesKey = "resolved_capabilities";

    private static readonly HashSet<string> ReservedAuthContextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        AuthenticatedUserIdKey,
        AuthenticatedRolesKey,
        ResolvedCapabilitiesKey,
        "user_id",
        "userId",
        "roles",
        "capabilities"
    };

    public static EndpointRequestDto ApplyJwtAuthority(
        EndpointRequestDto request,
        string jwtSubject,
        string jwtRole,
        IReadOnlyCollection<string> jwtCapabilities,
        string? routingRole = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(jwtSubject)) throw new ArgumentException("JWT subject is required.", nameof(jwtSubject));
        if (string.IsNullOrWhiteSpace(jwtRole)) throw new ArgumentException("JWT role is required.", nameof(jwtRole));

        var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.Context is not null)
        {
            foreach (var (key, value) in request.Context)
            {
                if (ReservedAuthContextKeys.Contains(key)) continue;
                context[key] = value;
            }
        }

        context[AuthenticatedUserIdKey] = jwtSubject;
        context[AuthenticatedRolesKey] = jwtRole;
        if (jwtCapabilities.Count > 0)
            context[ResolvedCapabilitiesKey] = string.Join(',', jwtCapabilities.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase));

        return request with
        {
            Role = routingRole ?? jwtRole,
            Context = context
        };
    }
}
