using JMS.Common;
using JMS.WebApi;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace JMS
{
    public interface IWebApiHostEnvironment
    {
        string AppSettingPath { get; }
        int Port { get; set; }
        ConfigurationValue<WebApiConfig> Config { get; set; }
        X509Certificate2 ServerCert { get;  set; }
        SslServerAuthenticationOptions SslServerAuthenticationOptions { get; } 
    }

    class DefaultWebApiHostEnvironment : IWebApiHostEnvironment
    {
        public string AppSettingPath { get; }
        public int Port { get; set; }
        public X509Certificate2 ServerCert { get; set; }
        public ConfigurationValue<WebApiConfig> Config { get; set; }

        public SslServerAuthenticationOptions SslServerAuthenticationOptions { get; private set; }

        public DefaultWebApiHostEnvironment(string appSettingPath,int port, ConfigurationValue<WebApiConfig> config)
        {
            AppSettingPath = appSettingPath;
            Port = port;
            Config = config;

            config.ValueChanged += Config_ValueChanged;

            resetSsl();
        }

        private void Config_ValueChanged(object sender, ValueChangedArg<WebApiConfig> e)
        {
            resetSsl();
        }

        void resetSsl()
        {

            if (this.SslServerAuthenticationOptions != null)
            {
                foreach (var cert in this.SslServerAuthenticationOptions.ServerCertificateContext.IntermediateCertificates)
                {
                    cert.Dispose();
                }
            }

            if (Config.Current.SSL != null && !string.IsNullOrEmpty(Config.Current.SSL.Cert))
            {
                this.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(Config.Current.SSL.Cert, Config.Current.SSL.Password);

                this.SslServerAuthenticationOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificateContext = SslStreamCertificateContext.Create(ServerCert, null),
                    RemoteCertificateValidationCallback = RemoteCertificateValidationCallback,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = Config.Current.SSL.SslProtocol== null? SslProtocols.None : Config.Current.SSL.SslProtocol.Value
                };
            }
            else
            {
                this.ServerCert = null;
                this.SslServerAuthenticationOptions = null;
            }
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            var acceptCertHash = Config.Current.SSL.AcceptCertHash;
            if (acceptCertHash != null && acceptCertHash.Length > 0 && acceptCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
    }
}
