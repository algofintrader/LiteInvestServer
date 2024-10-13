using LiteInvestServer.Entity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LiteInvestServer
{

    public static class JwtHelper
    {
        public static string LoginKey = "loginUser";
    }

    public record AuthResponse(string Token, DateTime ExpirationTime);

    public class JwtOptions
    {
        public string SecretKey { get; set; }
        public int ExpireHours { get; set; }
    }

    //TODO:ReSubscribe
    public class JwtProvider(JwtOptions _options)
    {
      
        private JwtOptions jwtoptions { get; set; } = _options;

        public AuthResponse GenerateToken(User user)
        {
            Claim[] claims = [new(JwtHelper.LoginKey, user.Login)];

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtoptions.SecretKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken
                (claims:claims,
                signingCredentials: signingCredentials,
                expires: DateTime.UtcNow.AddHours(jwtoptions.ExpireHours));

            var tokenvalue = new JwtSecurityTokenHandler().WriteToken(token);
          
            return new AuthResponse(tokenvalue,token.ValidTo);
        }

        public static TokenValidationParameters GetBasicTokenValidationParameters(JwtOptions _jwtOptions)
            => new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions!.SecretKey))
        };

        public static JwtSecurityToken ValidateToken(string token, JwtOptions _jwtOptions)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, GetBasicTokenValidationParameters(_jwtOptions), out SecurityToken validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken;
        }

    }

    static class JwtBearerExtensions
    {
        internal static string GetUserName(this HttpContext ctx)
            => ctx.User.FindFirstValue("loginUser")
            ?? throw new UnauthorizedAccessException("User authentication failed.");
    }

}
