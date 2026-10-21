using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DocsBrain.Api.Tenancy;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "docs-brain";
    public string Key { get; set; } = "";
}

public static class JwtSetup
{
    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.Key));

    public static string IssueToken(JwtOptions options, Guid tenantId, string user)
    {
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user),
                new Claim(HttpTenantContext.ClaimType, tenantId.ToString()),
            ],
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(SigningKey(options), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
