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
using System.Security.Cryptography.X509Certificates;

namespace JMS.MapShareFiles
{
    class MapFileManager
    {
        class MapItem
        {
            public string LocalPath;
            public Action<string, string> Callback;
        }

        Dictionary<string, MapItem> _dict = new Dictionary<string, MapItem>();
        NetAddress _gatewayAddress;

        MicroServiceHost _microServiceHost;
        ILogger<MapFileManager> _logger;
        public MapFileManager(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }

        public void GetGatewayShareFile(NetAddress gatewayAddr, string filepath, string localFilePath , X509Certificate2 gatewayClientCert)
        {
            using (var client = new CertClient(gatewayAddr, gatewayClientCert))
            {
                client.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.GetShareFile,
                    Content = filepath
                });

                var ret = client.ReadServiceObject<InvokeResult<int?>>();
                if (ret.Success == false)
                    throw new Exception(ret.Error);

                int len = ret.Data.GetValueOrDefault();
                var data = client.ReceiveDatas(len);
 
                File.WriteAllBytes(localFilePath, data);
            }
        }
        public void MapShareFileToLocal( NetAddress gatewayAddress, string shareFilePath, string localFilePath,Action<string, string> callback)
        {
            if (_gatewayAddress != null && _gatewayAddress.Equals(gatewayAddress.Address , gatewayAddress.Port) == false)
            {
                throw new Exception("不能监测不同网关的文件");
            }
            _dict[shareFilePath] = new MapItem { 
                LocalPath = localFilePath,
                Callback = callback
            };

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
                                    var item = _dict[filepath];
                                    string localpath = item.LocalPath;
                                    File.WriteAllBytes(localpath, data);
                                    if (item.Callback != null)
                                        item.Callback(filepath, localpath);
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
