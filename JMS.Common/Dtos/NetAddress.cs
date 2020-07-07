using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common.Dtos
{
    public class NetAddress
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public bool Equals(string ip ,int port)
        {
            return Address == ip && Port == port;
        }
    }
}
