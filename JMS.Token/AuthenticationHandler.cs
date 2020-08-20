using JMS.Token;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS
{
    class AuthenticationHandler : IAuthenticationHandler
    {
        public static string HeaderName;
        public static string ServerAddress;
        public static int ServerPort;
        public static X509Certificate2 Cert;
        public static AuthorizationContentType AuthorizationContentType;
        public object Authenticate(IDictionary<string, string> headers)
        {
            if (headers.ContainsKey(HeaderName) == false)
                throw new AuthenticationException("Authentication failed");

            var token = headers[HeaderName];
            TokenClient client = new TokenClient(ServerAddress, ServerPort, Cert);

            try
            {
                
                if (AuthorizationContentType == AuthorizationContentType.Longs)
                {
                   return client.VerifyForLongs(token);
                }
                else
                {
                    return client.VerifyForString(token);
                }
            }
            catch
            {
                throw new AuthenticationException("Authentication failed");
            }
        }
    }
}
