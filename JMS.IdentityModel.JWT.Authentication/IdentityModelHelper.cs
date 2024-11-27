using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;

namespace JMS.IdentityModel.JWT.Authentication
{
    class IdentityModelHelper
    {
        public static RsaSecurityKey GetSecurityKey(string serverUrl)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            var serverContnt = client.GetStringAsync($"{serverUrl}.well-known/openid-configuration").GetAwaiter().GetResult().FromJson<Dictionary<string, object>>();
            var keyContent = client.GetStringAsync(serverContnt["jwks_uri"].ToString()).GetAwaiter().GetResult();


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
            return new RsaSecurityKey(rsa);
        }
    }
}
