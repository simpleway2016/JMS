using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace JMS.IdentityModel.JWT.Authentication
{
    public class JwtHelper
    {
        /// <summary>
        /// JWT验证token
        /// </summary>
        /// <param name="jwtKey">jwt密钥</param>
        /// <param name="token">待验证的token</param>
        /// <returns>返回身份信息，验证失败则抛出异常</returns>
        public static ClaimsPrincipal Authenticate(string jwtKey,string token)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            return Authenticate(key, token);
           
        }

        public static ClaimsPrincipal Authenticate(SecurityKey jwtKey, string token)
        {
            SecurityToken validatedToken;
            var validator = new JwtSecurityTokenHandler();
            //建议由使用者使用JwtSecurityTokenHandler.DefaultMapInboundClaims设置
            //validator.MapInboundClaims = false;//保留所有Claim

            // These need to match the values used to generate the token
            TokenValidationParameters validationParameters = new TokenValidationParameters();
            validationParameters.IssuerSigningKey = jwtKey;
            validationParameters.ValidateIssuerSigningKey = true;
            validationParameters.ValidateAudience = false;
            validationParameters.ValidateIssuer = false;
            validationParameters.ValidateLifetime = false;


            if (validator.CanReadToken(token))
            {
                ClaimsPrincipal principal;
                // This line throws if invalid
                principal = validator.ValidateToken(token, validationParameters, out validatedToken);

                var expValue = principal.FindFirst("exp")?.Value;
                if (expValue != null)
                {
                    var utcnow = DateTimeOffset.Now.ToUnixTimeSeconds();

                    if (Convert.ToInt64(expValue) < utcnow)
                        throw new AuthenticationException("Authentication failed, out of date.");
                }
                return principal;
            }
            else
            {
                throw new AuthenticationException("Authentication failed, invalid token.");
            }


        }


        /// <summary>
        /// 生成JWT Token
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="jwtKey"></param>
        /// <param name="expireTime"></param>
        /// <returns></returns>
        public static string GenerateToken(Claim[] claims,string jwtKey,DateTime expireTime)
        {
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            return GenerateToken(claims, signingKey, expireTime);
        }

        /// <summary>
        /// 生成JWT Token,默认使用HS256(HmacSha256)
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="jwtKey"></param>
        /// <param name="expireTime"></param>
        /// <returns></returns>
        public static string GenerateToken(Claim[] claims, SecurityKey jwtKey, DateTime expireTime)
        {
         
            // Create the JWT and write it to a string
            var jwt = new JwtSecurityToken(
                claims: claims,
                expires: expireTime,
                signingCredentials: new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256));
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
            return encodedJwt;
        }

       /// <summary>
       /// 使用指定算法生成token
       /// </summary>
       /// <param name="securityAlgorithms">指定算法，如：RS256、HS256</param>
       /// <param name="claims"></param>
       /// <param name="jwtPrivateKey"></param>
       /// <param name="expireTime"></param>
       /// <returns></returns>

        public static string GenerateToken(string securityAlgorithms, Claim[] claims, SecurityKey jwtPrivateKey, DateTime expireTime)
        {

            // Create the JWT and write it to a string
            var jwt = new JwtSecurityToken(
                claims: claims,
                expires: expireTime,
                signingCredentials: new SigningCredentials(jwtPrivateKey, securityAlgorithms));
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
            return encodedJwt;
        }
    }
}
