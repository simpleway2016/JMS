using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
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
        public NetAddress(string addr, int port,bool useSsl)
        {
            this.Address = addr;
            this.Port = port;
            this.UseSsl = useSsl;
        }
        public NetAddress(string addr, int port, X509Certificate2  certificate)
        {
            this.Address = addr;
            this.Port = port;
            this.UseSsl = certificate != null?true:false;
            this.Certificate = certificate;
        }
        public string Address { get; set; }
        public int Port { get; set; }

        bool? _useSsl;
        /// <summary>
        /// 目标地址是否使用ssl加密
        /// </summary>
        public bool UseSsl
        {
            get
            {
                if(_useSsl == null)
                {
                    if (!string.IsNullOrWhiteSpace(this.ClientCertPath))
                        return true;
                    else if (this.Certificate != null)
                        return true;
                    else
                        return false;
                }
                return _useSsl.GetValueOrDefault();
            }
            set
            {
                _useSsl = value;
            }
        }

        /// <summary>
        /// 用指定的pfx证书连接目标地址
        /// </summary>
        public string ClientCertPath { get; set; }

        /// <summary>
        /// 证书密码
        /// </summary>
        public string ClientCertPassword { get; set; }

        /// <summary>
        /// 证书对应的域名
        /// </summary>
        public string CertDomain { get; set; }
        X509Certificate2 _Certificate;
        /// <summary>
        /// ClientCertPath证书生成的对象
        /// </summary>
        public X509Certificate2 Certificate
        {
            get
            {
                if(_Certificate == null && !string.IsNullOrWhiteSpace(this.ClientCertPath))
                {
                    _Certificate = new X509Certificate2(ClientCertPath, ClientCertPassword);
                }
                return _Certificate;
            }
            set
            {
                _Certificate = value;
            }
        }

        public override string ToString()
        {
            return $"{Address}:{Port}";
        }

        public override bool Equals(object obj)
        {
            NetAddress compaire = (NetAddress)obj;
            return this.Equals(compaire.Address , compaire.Port);
        }
        public bool Equals(string ip ,int port)
        {
            return Address == ip && Port == port;
        }

        public static bool operator ==(NetAddress a, NetAddress b)
        {
            if ((object)a == null && (object)b == null)
                return true;
            else if ((object)a == null || (object)b == null)
                return false;

            return a.Equals(b.Address, b.Port);
        }

        public static bool operator !=(NetAddress a, NetAddress b)
        {
            if ((object)a == null && (object)b == null)
                return false;
            else if ((object)a == null || (object)b == null)
                return true;

            return !a.Equals(b.Address, b.Port);
        }
    }
}
