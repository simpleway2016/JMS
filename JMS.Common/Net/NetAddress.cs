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
       
        public override bool Equals(object obj)
        {
            NetAddress compaire = (NetAddress)obj;
            return this.Equals(compaire.Address , compaire.Port);
        }
        public bool Equals(string ip ,int port)
        {
            return Address == ip && Port == port;
        }
    }
}
