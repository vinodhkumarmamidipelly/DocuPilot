using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Kiota.Authentication.Azure;
using Azure.Core;
using System.IdentityModel.Tokens.Jwt;

namespace SMEPilot.FunctionApp.Helpers
{
    public static class UserContextHelper
    {
        public static async Task<string?> GetTenantIdFromTokenAsync(string bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return null;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(bearerToken);
                var tid = token.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
                return tid;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<(string? UserId, string? TenantId, string? Email)> GetUserContextAsync(string bearerToken, Config cfg)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return (null, null, null);

            try
            {
                var tenantId = await GetTenantIdFromTokenAsync(bearerToken);
                if (string.IsNullOrWhiteSpace(tenantId))
                    return (null, null, null);

                var tokenCredential = new StaticTokenCredential(new AccessToken(bearerToken, DateTimeOffset.UtcNow.AddHours(1)));
                var authProvider = new AzureIdentityAuthenticationProvider(tokenCredential, scopes: new[] { "https://graph.microsoft.com/.default" });
                var graphClient = new GraphServiceClient(authProvider);
                var user = await graphClient.Me.GetAsync();

                return (user.Id, tenantId, user.Mail ?? user.UserPrincipalName);
            }
            catch
            {
                var tenantId = await GetTenantIdFromTokenAsync(bearerToken);
                return (null, tenantId, null);
            }
        }
    }
}

