using JMS.Common.Dtos;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class GatewayRefereeClient
    {
        NetAddress _netAddress;
        ILogger<GatewayRefereeClient> _logger;
        /// <summary>
        /// 记录当前网关是否是master
        /// </summary>
        public bool IsMaster { get; private set; }
        public GatewayRefereeClient(IConfiguration configuration, ILogger<GatewayRefereeClient> logger)
        {
            _netAddress = configuration.GetSection("Cluster:Referee").Get<NetAddress>();
            _logger = logger;
            if (_netAddress == null)
                this.IsMaster = true;
        }

        public void AddMicroService(RegisterServiceInfo service)
        {
            if (_netAddress == null)
                return;
            using (NetClient client = new NetClient(_netAddress.Address, _netAddress.Port))
            {
                client.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.RegisterSerivce,
                    Content = service.ToJsonString()
                });
                var cmd = client.ReadServiceObject<InvokeResult>();
                if (cmd.Success == false)
                    throw new Exception("not master");
            }
        }

        public void RemoveMicroService(RegisterServiceInfo service)
        {
            if (_netAddress == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    using (NetClient client = new NetClient(_netAddress.Address, _netAddress.Port))
                    {
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = CommandType.UnRegisterSerivce,
                            Content = service.ToJsonString()
                        });
                        var cmd = client.ReadServiceObject<InvokeResult>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);

                }
            });
        }
    }
}
