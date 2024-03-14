using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    class ServiceRunningItem
    {
        ClientCheckFactory _clientCheckProxyFactory;
        ILogger _logger;

        public ServiceRunningItem(ILogger logger, ClientCheckFactory clientCheckProxyFactory)
        {
            this._clientCheckProxyFactory = clientCheckProxyFactory;
            this._logger = logger;

        }

        private RegisterServiceInfo _ServiceInfo;
        public RegisterServiceInfo ServiceInfo
        {
            get => _ServiceInfo;
            set
            {
                _ServiceInfo = value;

                if (!string.IsNullOrEmpty(value.ClientCheckCodeFile) && File.Exists(value.ClientCheckCodeFile))
                {
                    var clientCheckCode = File.ReadAllText(value.ClientCheckCodeFile, Encoding.UTF8);

                    try
                    {
                        this.ClientChecker = _clientCheckProxyFactory.Create(clientCheckCode);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "客户端检验代码编译错误，代码：{0}", clientCheckCode);
                    }
                }
                else
                {
                    this.ClientChecker = null;
                }
            }
        }

        public IClientCheck ClientChecker { get; private set; }

    }
}
