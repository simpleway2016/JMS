using Extreme.Net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using JMS.Common.Net;
using Extreme.Net.Core.Proxy;
using JMS.IdentityModel.JWT.Authentication;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using JMS.Token;

namespace UnitTest
{
    [TestClass]
    public class JwtTest
    {
        [TestMethod]
        public void TokenClientTest()
        {
            var tokenClient = new TokenClient(new JMS.NetAddress("127.0.0.1", 9911));
            var token = tokenClient.Build("r:123", DateTime.Now.AddMinutes(20));
            tokenClient.Verify(token);
        }

        [TestMethod]
        public void Test()
        {

            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            var secretKey = "mysuperret_secretkey!123";

            var claims = new Claim[]
   {
        new Claim(JwtRegisteredClaimNames.Sub, "Jack.T"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
   };

            var time = DateTime.Now.AddMinutes(3);
            
            var token = JwtHelper.GenerateToken(claims, secretKey, time);
            var userinfo = JwtHelper.Authenticate(secretKey, token);

            var subValue = userinfo.FindFirstValue(JwtRegisteredClaimNames.Sub);
            foreach ( var claim in userinfo.Claims)
            {
                var key = claim.Type;
                var value = claim.Value;
            }
        }

    }
}
