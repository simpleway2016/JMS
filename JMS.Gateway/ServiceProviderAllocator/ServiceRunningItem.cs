using JMS.Dtos;
using Microsoft.Extensions.Logging;
using Natasha.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    class ServiceRunningItem
    {
        ClientCheckProxyFactory _clientCheckProxyFactory;
        ILogger _logger;

        public ServiceRunningItem(ILogger logger, ClientCheckProxyFactory clientCheckProxyFactory)
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

                if (!string.IsNullOrEmpty(value.ClientCheckCode))
                {
                    if (this.ClientChecker != null && this.ClientChecker.ClientCode == value.ClientCheckCode)
                    {

                    }
                    else
                    {
                        try
                        {
                            
                            if (this.ClientChecker != null)
                            {
                                this.ClientChecker.Dispose();
                            }

                            this.ClientChecker = _clientCheckProxyFactory.Create(value.ClientCheckCode);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "客户端检验代码编译错误，代码：{0}", value.ClientCheckCode);
                        }
                    }
                }
                else
                {
                    if (this.ClientChecker != null)
                    {
                        this.ClientChecker.Dispose();
                    }
                    this.ClientChecker = null;
                }
            }
        }

        public ClientCheckProxy ClientChecker { get; private set; }

    }
}
