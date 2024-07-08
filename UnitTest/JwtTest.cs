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
using Way.Lib;
using System.Collections.Concurrent;

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

        class NetClientSeatCollection
        {
            public event EventHandler Init;
            int _state = 0;//0=未初始化 1=准备初始化 2=初始化完毕

            public void init()
            {
                if (_state == 2)
                    return;
                if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
                {
                    Init(this, null);
                    _state = 2;
                }
                else
                {
                    while (_state != 2)
                        Thread.Sleep(10);
                }
            }

        }

        [TestMethod]
        public void test22()
        {
            int count = 0;
            ConcurrentDictionary<(string, int), NetClientSeatCollection> dict = new ConcurrentDictionary<(string, int), NetClientSeatCollection>();

            Parallel.For(0, 100, index => { 
                for(int i = 0; i < 10; i ++)
                {
                    var key = ("abc", i);
                    var list = dict.GetOrAdd(key, s => new NetClientSeatCollection());

                    list.Init += (s2, e) => {
                        Interlocked.Increment(ref count);
                    };
                    list.init();
                }
            });

            Assert.AreEqual(dict.Count, 10);
            Assert.AreEqual(count, 10);
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
