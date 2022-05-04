using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS.IdentityModel.JWT.Authentication
{
    public class AuthenticationParameter
    {
        /// <summary>
        /// 待验证的token
        /// </summary>
        public string Token { get; }
        /// <summary>
        /// token解码后内容，可在回调中修改
        /// </summary>
        public object Content { get; set; }
        public AuthenticationParameter(string token)
        {
            this.Token = token;
        }
    }
    class AuthenticationHandler : IAuthenticationHandler
    {
        public static Func<AuthenticationParameter, bool> Callback;
        public static string HeaderName;
        public static string ServerUrl;
        public static int RefreshPublicKeySeconds;
        static RsaSecurityKey rsaSecurityKey;
        public AuthenticationHandler()
        {
            
        }

        internal static void GetPublicKey()
        {
            while(true)
            {
                try
                {
                    if (ServerUrl.EndsWith("/") == false)
                        ServerUrl += "/";
                    var serverContnt = Way.Lib.HttpClient.GetContent($"{ServerUrl}.well-known/openid-configuration", 8000).FromJson<Dictionary<string, object>>();
                    var keyContent = Way.Lib.HttpClient.GetContent(serverContnt["jwks_uri"].ToString(), 8000);


                    JsonWebKeySet exampleJWKS = new JsonWebKeySet(keyContent);
                    JsonWebKey exampleJWK = exampleJWKS.Keys.First();


                    /* Create RSA from Elements in JWK */
                    RSAParameters rsap = new RSAParameters
                    {
                        Modulus = WebEncoders.Base64UrlDecode(exampleJWK.N),
                        Exponent = WebEncoders.Base64UrlDecode(exampleJWK.E),
                    };
                    System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportParameters(rsap);
                    rsaSecurityKey = new RsaSecurityKey(rsa);

                    Thread.Sleep(RefreshPublicKeySeconds*1000);
                }
                catch (Exception)
                {

                    Thread.Sleep(1000);
                    continue;
                }
            }
        }

        public object Authenticate(IDictionary<string, string> headers)
        {
            for(int i = 0; i < 8 && rsaSecurityKey == null; i ++)
            {
                Thread.Sleep(1000);
            }
            if (headers.ContainsKey(HeaderName) == false)
                throw new AuthenticationException("Authentication failed");

            var token = headers[HeaderName];
            try
            {
                JsonWebToken exampleJWT = new JsonWebToken(token);

                var jwtTokenHandler = new JwtSecurityTokenHandler();
                var vaild = jwtTokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    IssuerSigningKey = rsaSecurityKey,
                    RequireExpirationTime = false,
                    RequireSignedTokens = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                }, out SecurityToken validatedSecurityToken);
                var ret = vaild;

                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    authParameter.Content = ret;
                    if (!Callback(authParameter))
                    {
                        throw new AuthenticationException("Authentication failed");
                    }
                    return authParameter.Content;
                }
                return ret;
            }
            catch (AuthenticationException e)
            {
                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    if (Callback(authParameter))
                    {
                        return authParameter.Content;
                    }                   
                }

                throw e;
            }
            catch
            {
                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    if (Callback(authParameter))
                    {
                        return authParameter.Content;
                    }
                }
                throw new AuthenticationException("Authentication failed");
            }
        }
    }
}
