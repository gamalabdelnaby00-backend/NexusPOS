using System.Security.Claims;

namespace UniversalPOS.Api.Tenancy;

public interface ITenantConnectionStringResolver
{
    string Resolve(ClaimsPrincipal authenticatedPrincipal);
}
