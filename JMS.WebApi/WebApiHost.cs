using JMS.Applications;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Way.Lib;

namespace JMS
{
    public class WebApiHost : IDisposable
    {
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ILogger<WebApiHost> _Logger;
        private readonly IWebApiHostEnvironment _webApiEnvironment;
        IRequestReception _requestReception;

      
        public IServiceProvider ServiceProvider { get; set; }

        internal bool Disposed { get; private set; }

        public WebApiHost(ILogger<WebApiHost> logger,IWebApiHostEnvironment webApiEnvironment)
        {
            _Logger = logger;
            _webApiEnvironment = webApiEnvironment;
            _Logger.LogInformation($"版本号：{this.GetType().Assembly.GetName().Version}");

            _Logger?.LogInformation("配置文件:{0}", _webApiEnvironment.AppSettingPath);
            _Logger?.LogInformation($"网关地址：{_webApiEnvironment.Config.Current.Gateways.ToJsonString()}");
        }

        string _id;
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    var configuration = ServiceProvider.GetService<IConfiguration>();
                    var datafolder = configuration.GetValue<string>("DataFolder");
                    var file = $"{datafolder}/GatewayId.txt";
                    if (File.Exists(file))
                    {
                        _id = File.ReadAllText(file, Encoding.UTF8);
                    }
                    else
                    {
                        _id = Guid.NewGuid().ToString("N");
                        File.WriteAllText(file, _id, Encoding.UTF8);
                    }
                }
                return _id;
            }
        }

        public void Run()
        {
            _requestReception = ServiceProvider.GetService<IRequestReception>();
            _tcpServer = new JMS.ServerCore.MulitTcpListener(_webApiEnvironment.Port, null);
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;

            _Logger?.LogInformation($"Listening {(_webApiEnvironment.ServerCert != null?"https":"http")}://*:{_webApiEnvironment.Port}");
            if (_webApiEnvironment.ServerCert != null)
            {
                _Logger?.LogInformation("Use ssl,certificate hash:{0}", _webApiEnvironment.ServerCert.GetCertHashString());
            }

            _tcpServer.Run();
        }

        private void _tcpServer_OnError(object sender, Exception e)
        {
            _Logger?.LogError(e , "");
        }

        private void _tcpServer_Connected(object sender, Socket socket)
        {
            Task.Run(() => _requestReception.Interview(socket));
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;

                if (_tcpServer != null)
                {
                    _tcpServer.Stop();
                    _tcpServer = null;
                }
            }
        }
    }
}
