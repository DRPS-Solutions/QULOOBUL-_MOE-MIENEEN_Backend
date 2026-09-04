using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth
{
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