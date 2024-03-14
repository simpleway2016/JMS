﻿
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JMS.Dtos;
using Way.Lib;
using Org.BouncyCastle.Crypto.Engines;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace JMS
{
    public class ShareFileClient
    {
        NetAddress _proxy;
        class MapItem
        {
            public string LocalPath;
            public Action<string, string> Callback;
        }

        Dictionary<string, MapItem> _dict = new Dictionary<string, MapItem>();

        NetAddress _gatewayAddress;
        ILogger _logger;
      
        public ShareFileClient(NetAddress gatewayAddress, NetAddress proxy = null, ILogger logger = null)
        {
            this._proxy = proxy;

            this._gatewayAddress = gatewayAddress;
            this._logger = logger;
        }

        /// <summary>
        /// 获取网关共享文件，并保存到本地
        /// </summary>
        /// <param name="filepath">共享文件路径</param>
        /// <param name="localFilePath">保存到本地的路径</param>
        public void GetGatewayShareFile( string filepath, string localFilePath )
        {
            using (var client = new ProxyClient(_proxy))
            {
                client.Connect(_gatewayAddress);
                client.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.GetShareFile,
                    Content = filepath
                });

                var ret = client.ReadServiceObject<InvokeResult<int?>>();
                if (ret.Success == false)
                    throw new Exception(ret.Error);

                int len = ret.Data.GetValueOrDefault();
                var data = new byte[len];
                client.ReadData(data, 0, len );

                File.WriteAllBytes(localFilePath, data);
            }
        }

        /// <summary>
        /// 映射网关上的共享文件到本地
        /// </summary>
        /// <param name="shareFilePath">共享文件路径</param>
        /// <param name="localFilePath">映射本地的路径</param>
        /// <param name="callback">文件写入本地后，回调委托</param>
        public void MapShareFileToLocal(string shareFilePath, string localFilePath,Action<string, string> callback = null)
        {
            _dict[shareFilePath] = new MapItem { 
                LocalPath = localFilePath,
                Callback = callback
            };
        }

        /// <summary>
        /// 开始监听文件变化
        /// </summary>
        public void StartListen()
        {
            if(_dict.Keys.Count > 0)
                new Thread(connect).Start();
        }

        void connect()
        {
            while (true)
            {
                try
                {
                    using (var client = new ProxyClient(_proxy))
                    {
                        client.Connect(_gatewayAddress);
                        client.WriteServiceData(new GatewayCommand { 
                            Type = (int)CommandType.ListenFileChange,
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
                                var data = new byte[len];
                                client.ReadData(data,0,len);

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
                    _logger?.LogError(ex, "文件监听异常");
                }
                Thread.Sleep(1000);
            }
        }
    }
}
