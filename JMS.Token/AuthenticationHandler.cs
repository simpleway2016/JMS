using JMS.Token;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Way.Lib;

namespace JMS
{
   
    class AuthenticationHandler : IAuthenticationHandler
    {
        public static Func<string, object, bool> Callback;
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
                object ret;
                if (AuthorizationContentType == AuthorizationContentType.Long)
                {
                    ret = client.VerifyLong(token);
                }
                else
                {
                    ret = client.VerifyString(token);
                }

                if (Callback != null)
                {
                    if (!Callback(token, ret))
                    {
                        throw new AuthenticationException("Authentication failed");
                    }
                }
                return ret;
            }
            catch(AuthenticationException e)
            {
                throw e;
            }
            catch
            {
                throw new AuthenticationException("Authentication failed");
            }
        }
    }
}
