using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth
{
    /// <summary>
    /// Issues the signed (HMAC-SHA256) access token UC-01 returns: user id, name, role, and one
    /// "perm" claim per granted permission, which Program.cs's authorization policies check.
    /// Reference: Jones, M., Bradley, J. and Sakimura, N. (2015) RFC 7519: JSON Web Token (JWT). Internet Engineering Task Force. Available at: https://www.rfc-editor.org/info/rfc7519 (Accessed: 8 August 2026).
    /// Reference: Microsoft (2026) Claims-based authorization in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/claims (Accessed: 4 October 2026).
    /// </summary>
    public class JwtTokenFactory(IConfiguration config)
    {
        public (string token, DateTime expiresUtc) Create(User user, string roleName, IEnumerable<string> permissions)
        {
            var jwt = config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["LifetimeMinutes"] ?? "60"));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, $"{user.FirstName} {user.Surname}"),
                new(ClaimTypes.Role, roleName),
            };
            claims.AddRange(permissions.Select(p => new Claim("perm", p)));

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"], audience: jwt["Audience"],
                claims: claims, expires: expires,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//