using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Linq;

namespace JMS.Proxy
{
    class RequestHandler
    {
        Proxy _proxy;
        public RequestHandler(Proxy proxy)
        {
            _proxy = proxy;
        }
        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_proxy.AcceptCertHash != null && _proxy.AcceptCertHash.Length > 0 && _proxy.AcceptCertHash.Contains(certificate.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
        public async void Interview(Socket socket)
        {
            try
            {
                var client = new NetClient(socket);
                if (_proxy.ServerCert != null)
                {
                    var sslts = new SslStream(client.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
                    await sslts.AuthenticateAsServerAsync(_proxy.ServerCert, true, NetClient.SSLProtocols, false);
                    client.InnerStream = sslts;
                }

                using (var con = new Connect(client , _proxy))
                {
                    await con.Start();
                }
            }
            catch
            {
            }
        }
    }
}
