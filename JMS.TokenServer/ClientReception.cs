using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.TokenServer
{
    class ClientReception
    {
        NetStream _netStream;
        public ClientReception(NetStream netStream)
        {
            this._netStream = netStream;
        }

        public void OnTokenDisabled(string token, long utcExpireTime)
        {
            lock (this)
            {
                _netStream.Write(true);
                _netStream.Write(utcExpireTime);
                var data = Encoding.UTF8.GetBytes(token);
                _netStream.Write(data.Length);
                _netStream.Write(data);
            }
        }

        public void Handle()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(10000);
                lock (this)
                {
                    _netStream.Write(false);
                }
            }
        }
    }
}
