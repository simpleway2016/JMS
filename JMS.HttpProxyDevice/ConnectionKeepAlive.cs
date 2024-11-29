using JMS.Common.Security;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxyDevice
{
    /// <summary>
    /// 和服务器保持足够的连接数
    /// </summary>
    public class ConnectionKeepAlive
    {
        private readonly ILogger<ConnectionKeepAlive> _logger;
        private readonly ConnectionHandler _connectionHandler;
        int _waitingConnectionCount = 0;

        public int WaitingConnectionCount => _waitingConnectionCount;

        int _dataConnectionCount = 0;

        public ConnectionKeepAlive(ILogger<ConnectionKeepAlive> logger , ConnectionHandler connectionHandler)
        {
            for(int i = 0; i < HttpProxyDeviceProgram.Config.Current.Device.ConnectionCount; i++)
            {
                createConnection();
            }
            _logger = logger;
            _connectionHandler = connectionHandler;
        }

        async void createConnection()
        {
            bool reConnect = true;
            bool dataConnected = false;
            Interlocked.Increment(ref _waitingConnectionCount);

            try
            {
                using var netClient = new NetClient();
                await netClient.ConnectAsync(HttpProxyDeviceProgram.Config.Current.ProxyServer);
                netClient.ReadTimeout = 0;

                netClient.WriteLine(HttpProxyDeviceProgram.Config.Current.Device.Name);

                if (HttpProxyDeviceProgram.Config.Current.Device.Password.Length > 32)
                    HttpProxyDeviceProgram.Config.Current.Device.Password = HttpProxyDeviceProgram.Config.Current.Device.Password.Substring(0, 32);
                else if (HttpProxyDeviceProgram.Config.Current.Device.Password.Length < 32)
                {
                    HttpProxyDeviceProgram.Config.Current.Device.Password = HttpProxyDeviceProgram.Config.Current.Device.Password.PadRight(32, '0');
                }

                var content = AES.Encrypt(HttpProxyDeviceProgram.Config.Current.Device.Name, HttpProxyDeviceProgram.Config.Current.Device.Password);
                netClient.WriteLine(content);

                //获取代理的端口
                var port = await netClient.ReadIntAsync();
                if(HttpProxyDeviceProgram.Config.Current.AllowPorts != null && HttpProxyDeviceProgram.Config.Current.AllowPorts.Length > 0)
                {
                    if(HttpProxyDeviceProgram.Config.Current.AllowPorts.Contains(port) == false)
                    {
                        _logger.LogInformation($"服务器访问了不允许的端口：{port}");
                        return;
                    }
                }
                if (HttpProxyDeviceProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"要求代理{port}端口");
                }

                reConnect = false;
                Interlocked.Decrement(ref _waitingConnectionCount);

                createConnection();//这个连接开始处理数据，新开一个连接等候，这样可以保持等候连接的数量

                dataConnected = true;
                Interlocked.Increment(ref _dataConnectionCount);

                if (HttpProxyDeviceProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"等候连接数={_waitingConnectionCount}  数据连接数={_dataConnectionCount}");
                }

                await _connectionHandler.Handle(netClient , port);
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
                if (reConnect)
                {
                    Interlocked.Decrement(ref _waitingConnectionCount);
                    createConnection();
                }

                if (dataConnected)
                {
                    Interlocked.Decrement(ref _dataConnectionCount);
                }
            }

        }

        public async Task RunAsync()
        {
            await Task.Delay(3000);
            while (true)
            {
                if(HttpProxyDeviceProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"等候连接数={_waitingConnectionCount}  数据连接数={_dataConnectionCount}");
                    await Task.Delay(3000);
                }
                else
                {
                    _logger.LogInformation($"等候连接数={_waitingConnectionCount}  数据连接数={_dataConnectionCount}");
                    await Task.Delay(60000);
                }
            }               
        }
    }
}
