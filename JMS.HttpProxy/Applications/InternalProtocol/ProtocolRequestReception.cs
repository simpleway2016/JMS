using JMS.Common;
using JMS.Common.Security;
using JMS.HttpProxy.InternalProtocol;
using JMS.HttpProxy.Servers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications.InternalProtocol
{
    public class ProtocolRequestReception
    {
        InternalProtocolServer _protocolServer;
        private readonly ILogger<ProtocolRequestReception> _logger;
        private readonly BlackList _blackList;
        private readonly InternalConnectionProvider _connectionProvider;

        public ProtocolRequestReception(ILogger<ProtocolRequestReception> logger, BlackList blackList,
            InternalConnectionProvider connectionProvider)
        {
            _logger = logger;
            _blackList = blackList;
            _connectionProvider = connectionProvider;
            _blackList.SetKeepMinutes(120);//设置黑名单时间为120分钟
        }

        public void SetServer(InternalProtocolServer server)
        {
            _protocolServer = server;
        }

        public async void Interview(Socket socket)
        {
            NetClient client = null;
            try
            {
                var ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
                if (_blackList.CheckBlackList(ip) == false)
                {
                    if (HttpProxyProgram.Config.Current.LogDetails)
                    {
                        _logger.LogInformation($"{ip} in black list");
                    }
                    socket.Dispose();
                    return;
                }

                client = new NetClient(socket);
                var name = await client.ReadLineAsync(1024);
                var value = await client.ReadLineAsync(1024);

                var deviceConfig = HttpProxyProgram.Config.Current.Devices.FirstOrDefault(m => m.Name == name);
                if (deviceConfig == null)
                {
                    _blackList.MarkError(ip);
                    return;
                }

                try
                {
                    if (deviceConfig.Password.Length > 32)
                        deviceConfig.Password = deviceConfig.Password.Substring(0, 32);
                    else if(deviceConfig.Password.Length < 32)
                    {
                        deviceConfig.Password = deviceConfig.Password.PadRight(32, '0');
                    }

                    if (AES.Decrypt(value, deviceConfig.Password) != name)
                    {
                        _blackList.MarkError(ip);
                        return;
                    }
                }
                catch 
                {
                    _blackList.MarkError(ip);
                    return;
                }

                if(name.Contains(":"))
                {
                    _logger.LogError($"{name}包含冒号");
                    return;
                }
                _connectionProvider.AddConnection(name, client);
                client = null;
            }
            catch (SocketException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            catch (IOException ex)
            {
                if (ex.HResult != -2146232800)
                {
                    _logger?.LogError(ex, "");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "");
            }
            finally
            {
                client?.Dispose();
            }

        }
    }
}
