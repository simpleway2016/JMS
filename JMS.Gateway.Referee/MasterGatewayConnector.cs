using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Way.Lib;

namespace JMS.Gateway
{
    /// <summary>
    /// 保持与master网关的连接
    /// </summary>
    class MasterGatewayConnector
    {
        Referee _referee;
        ILogger<MasterGatewayConnector> _logger;
        public MasterGatewayConnector(Referee referee, ILogger<MasterGatewayConnector> logger)
        {
            _referee = referee;
            _logger = logger;
        }

        public void Start(NetClient netclient)
        {
            netclient.ReadTimeout = 30000;
            while (true)
            {
                try
                {
                    var cmd = netclient.ReadServiceObject<GatewayCommand>();
                    netclient.WriteServiceData(new InvokeResult { 
                        Success = true
                    });
                }
                catch(SocketException)
                {
                    _logger?.LogInformation("主网关{0}断开", _referee.MasterIp?.ToJsonString());
                    _referee.MasterIp = null;
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);                    
                    _referee.MasterIp = null;
                    return;
                }
                
            }
        }
    }
}
