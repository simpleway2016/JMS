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
        Socks5Server _proxy;
        public RequestHandler(Socks5Server proxy)
        {
            _proxy = proxy;
        }
     
        public async void Interview(Socket socket)
        {
            try
            {
                var client = new NetClient(socket);

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
