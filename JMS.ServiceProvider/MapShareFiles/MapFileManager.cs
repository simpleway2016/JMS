using JMS.Common.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using JMS.Net;
using JMS.Dtos;
using Way.Lib;
using Org.BouncyCastle.Crypto.Engines;
using Microsoft.Extensions.Logging;
using System.IO;

namespace JMS.MapShareFiles
{
    class MapFileManager
    {
        Dictionary<string, string> _dict = new Dictionary<string, string>();
        NetAddress _gatewayAddress;

        MicroServiceHost _microServiceHost;
        ILogger<MapFileManager> _logger;
        public MapFileManager(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }
        public void MapShareFileToLocal( NetAddress gatewayAddress, string shareFilePath, string localFilePath)
        {
            if (_gatewayAddress != null && _gatewayAddress.Equals(gatewayAddress.Address , gatewayAddress.Port) == false)
            {
                throw new Exception("不能监测不同网关的文件");
            }
            _dict[shareFilePath] = localFilePath;

            _gatewayAddress = gatewayAddress;
        }

        internal void Start()
        {
            if(_dict.Keys.Count > 0)
                new Thread(connect).Start();
        }

        void connect()
        {
            SSLConfiguration sSLConfiguration = null;
            while (true)
            {
               if(_microServiceHost.ServiceProvider == null || _microServiceHost.Id == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                if (sSLConfiguration == null)
                {
                    sSLConfiguration = _microServiceHost.ServiceProvider.GetService<SSLConfiguration>();
                    _logger = _microServiceHost.ServiceProvider.GetService<ILogger<MapFileManager>>();
                }
                try
                {
                    using (var client = new GatewayClient( _gatewayAddress , sSLConfiguration))
                    {
                        client.WriteServiceData(new GatewayCommand { 
                            Type = CommandType.ListenFileChange,
                            Content = _dict.Keys.ToJsonString()
                        });

                        client.ReadTimeout = 60000;
                        while (true)
                        {
                            var ret = client.ReadServiceObject<InvokeResult<string>>();
                            if(ret.Data != null)
                            {
                                string filepath = ret.Data;
                                _logger?.LogInformation("文件映射系统收到新的文件:{0}",filepath);
                                int len = client.ReadInt();
                                var data = client.ReceiveDatas(len);
                                try
                                {
                                    string localpath = _dict[filepath];
                                    File.WriteAllBytes(localpath, data);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, ex.Message);
                                }
                                
                            }

                            client.WriteServiceData(new InvokeResult());
                        }
                    }
                }
                catch (Exception ex)
                {
                }
                Thread.Sleep(1000);
            }
        }
    }
}
