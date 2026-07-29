using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TraineeManagement.api.Models;

namespace TraineeManagement.api.Helper
{
    public class JwtHelper
    {

        public static string GenerateJwtToken(UserModel user, IConfiguration _config)
        {
            var tokenHandler = new JsonWebTokenHandler();

            var jwtSettings = _config.GetSection("JwtSettings");

            if (jwtSettings == null) throw new Exception("Jwt is not configured!");

            var secretKey = jwtSettings["SecretKey"];

            var key = Encoding.UTF8.GetBytes(secretKey!);

            var expiry = jwtSettings["ExpiryMinutes"];

            var issuer = jwtSettings["Issuer"];

            var audience = jwtSettings["Audience"];

            if (issuer == null || audience == null || expiry == null || secretKey == null) throw new Exception("Jwt is not configured");

            var claims = new List<Claim>
            {
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(expiry)),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = issuer,
                Audience = audience
            };

            return tokenHandler.CreateToken(tokenDescriptor);

        }

        public static ClaimsPrincipal? ValidateToken(string token, IConfiguration _config)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var jwtSettings = _config.GetSection("JwtSettings");

            if (jwtSettings == null) throw new Exception("Jwt is not configured!");

            var secretKey = jwtSettings["SecretKey"];

            var key = Encoding.UTF8.GetBytes(secretKey!);

            var issuer = jwtSettings["Issuer"];

            var audience = jwtSettings["Audience"];

            try
            {
                var principal = tokenHandler.ValidateToken(
                    token,
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,

                        ValidateAudience = true,
                        ValidAudience = audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),

                        ValidateLifetime = true,

                        ClockSkew = TimeSpan.Zero

                    },
                out _);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
