using LiteInvestServer.Entity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LiteInvestServer
{

    public class JwtOptions
    {
        public string SecretKey { get; set; }
        public int ExpireHours { get; set; }
    }

    public class JwtProvider(JwtOptions _options)
    {
        private JwtOptions jwtoptions { get; set; } = _options;

        public string GenerateToken(User user)
        {
            Claim[] claims = [new("loginUser", user.Login)];

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtoptions.SecretKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken
                (claims:claims,
                signingCredentials: signingCredentials,
                expires: DateTime.UtcNow.AddHours(jwtoptions.ExpireHours));

            var tokenvalue = new JwtSecurityTokenHandler().WriteToken(token);
            return tokenvalue;
        }

    }

    static class JwtBearerExtensions
    {
        internal static string GetUserName(this HttpContext ctx)
            => ctx.User.FindFirstValue("loginUser")
            ?? throw new UnauthorizedAccessException("User authentication failed.");
    }

}
