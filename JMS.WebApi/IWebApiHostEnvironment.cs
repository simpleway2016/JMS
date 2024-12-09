using JMS.Common;
using JMS.WebApi;
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
        SslProtocols SslProtocol { get; set; }
    }

    class DefaultWebApiHostEnvironment : IWebApiHostEnvironment
    {
        public string AppSettingPath { get; }
        public int Port { get; set; }
        public X509Certificate2 ServerCert { get; set; }
        public SslProtocols SslProtocol { get; set; } = SslProtocols.None;
        public ConfigurationValue<WebApiConfig> Config { get; set; }

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
            if (Config.Current.SSL != null && !string.IsNullOrEmpty(Config.Current.SSL.Cert))
            {
                this.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(Config.Current.SSL.Cert, Config.Current.SSL.Password);
                var sslProtocols = Config.Current.SSL.SslProtocol;
                if (sslProtocols != null)
                {
                    this.SslProtocol = sslProtocols.Value;
                }
                else
                {
                    this.SslProtocol = SslProtocols.None;
                }
            }
            else
            {
                this.ServerCert = null;
            }
        }
    }
}
