using System.Security.Claims;

namespace UniversalPOS.Api.Tenancy;

/// <summary>
/// Resolves a tenant database only from a tenant_id claim issued by the API's trusted authentication flow.
/// Never resolve database names or connection strings from an arbitrary request header or request body.
/// </summary>
public sealed class TenantConnectionStringResolver(IConfiguration configuration) : ITenantConnectionStringResolver
{
    public string Resolve(ClaimsPrincipal authenticatedPrincipal)
    {
        ArgumentNullException.ThrowIfNull(authenticatedPrincipal);
        if (authenticatedPrincipal.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("An authenticated user is required to resolve a tenant database.");

        var tenantId = authenticatedPrincipal.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new UnauthorizedAccessException("The authenticated identity has no tenant_id claim.");

        var connectionString = configuration[$"TenantDatabases:{tenantId}"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("No database is configured for the authenticated tenant.");

        return connectionString;
    }
}
