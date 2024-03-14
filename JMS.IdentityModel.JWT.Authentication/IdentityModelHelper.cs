using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.IdentityModel.JWT.Authentication
{
    class IdentityModelHelper
    {
        public static RsaSecurityKey GetSecurityKey(string serverUrl)
        {
            var serverContnt = Way.Lib.HttpClient.GetContent($"{serverUrl}.well-known/openid-configuration", 8000).FromJson<Dictionary<string, object>>();
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
            return new RsaSecurityKey(rsa);
        }
    }
}
