using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class NetAddress
    {
        public NetAddress()
        {
        }
        public NetAddress(string addr ,int port)
        {
            this.Address = addr;
            this.Port = port;
        }
        public string Address { get; set; }
        public int Port { get; set; }
        public bool Equals(string ip ,int port)
        {
            return Address == ip && Port == port;
        }
    }
}
