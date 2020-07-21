
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class GatewayRefereeClient
    {
        NetAddress _refereeAddress;
        ILogger<GatewayRefereeClient> _logger;
        LockKeyManager _lockKeyManager;
        ConcurrentDictionary<string, RegisterServiceLocation> _waitServiceList;
        Gateway _gateway;
        /// <summary>
        /// 记录当前网关是否是master
        /// </summary>
        public bool IsMaster { get; private set; }
        public GatewayRefereeClient(IConfiguration configuration,
            LockKeyManager lockKeyManager,
            Gateway gateway,
            ILogger<GatewayRefereeClient> logger)
        {
            _refereeAddress = configuration.GetSection("Cluster:Referee").Get<NetAddress>();
            _logger = logger;
            _lockKeyManager = lockKeyManager;
            _gateway = gateway;

            if (_refereeAddress == null)
            {
                _lockKeyManager.IsReady = true;
                this.IsMaster = true;
            }
            else
                new Thread(toBeMaster).Start();

            SystemEventCenter.MicroServiceUploadLockedKeyCompleted += SystemEventCenter_MicroServiceUploadLockedKeyCompleted;
        }

        private void SystemEventCenter_MicroServiceUploadLockedKeyCompleted(object sender, RegisterServiceInfo e)
        {
            _logger?.LogInformation($"{e.Host}:{e.Port} UploadLockedKeyCompleted");
               _waitServiceList?.TryRemove($"{e.Host}:{e.Port}",out RegisterServiceLocation o);
        }


        void toBeMaster()
        {
            /*
申请成为master -->
                    成为master     --> 维持心跳          --> 心跳断开后 -->重新连接裁判机申请成为master
                                                                                                        -->还是master ...
                                                                                                        -->如果不再是master --> 断开连接的所有微服务
                    不能成为master --> 和master维持心跳  --> 心跳断开后 -->重新连接裁判机申请成为master
             */


            NetAddress masterAddr = null;
            while(true)
            {              
                try
                {
                    if (!this.IsMaster)
                    {
                        _lockKeyManager.IsReady = false;
                    }

                    using (var client = new NetClient(_refereeAddress))
                    {
                        client.WriteServiceData(new GatewayCommand { 
                            Type = CommandType.ApplyToBeMaster,
                            Content = _gateway.Port.ToString()
                        });
                        var ret = client.ReadServiceObject<InvokeResult<string>>();

                        _logger?.LogInformation("与裁判连接成功,裁判返回数据：{0}",ret.Data);
                        if (ret.Success)
                        {
                            masterAddr = null;
                            if (this.IsMaster == false)
                            {
                                _logger?.LogInformation("成为主网关");
                                _waitServiceList = ret.Data.FromJson<ConcurrentDictionary<string, RegisterServiceLocation>>();
                                this.IsMaster = true;

                                //等待所有微服务上传locked key
                                for (int i = 0; i < 10 && _waitServiceList.Count > 0; i++)
                                {
                                    _logger?.LogInformation("还有{0}个微服务没有报到 {1}" , _waitServiceList.Count , _waitServiceList.Keys.ToArray().ToJsonString());
                                    Thread.Sleep(1000);
                                }
                                _lockKeyManager.IsReady = true;

                                if(_waitServiceList.Count > 0)
                                    _logger?.LogInformation("还有{0}个微服务没有报到，但被忽略了。", _waitServiceList.Count);
                                _logger?.LogInformation("lockKeyManager就绪");
                                _logger?.LogDebug("锁记录：{0}", _lockKeyManager.GetCaches().ToJsonString());
                            }

                            client.KeepHeartBeating();
                            _logger?.LogInformation("与裁判的连接断开");
                        }
                        else
                        {
                            if(this.IsMaster)
                            {
                                this.IsMaster = false;
                                _lockKeyManager.IsReady = false;
                                //不是主网关，需要断开所有微服务
                                var allservices = _gateway.OnlineMicroServices.ToArray();
                                foreach(var s in allservices)
                                    s.Close();
                            }
                            //另一个网关成为主网关
                            masterAddr = ret.Data.FromJson<NetAddress>();
                           
                        }
                    }

                    if(masterAddr != null)
                    {
                        _logger?.LogInformation("准备和主网关连接{0}",masterAddr.ToJsonString());
                        //连上主网关，直到连接出现问题，再申请成为主网关
                        using (var client = new NetClient(masterAddr))
                        {
                            _logger?.LogInformation("与主网关连接心跳");
                            client.KeepHeartBeating();
                            Thread.Sleep(100);
                            _logger?.LogInformation("与主网关连接断开");
                        }
                    }
                }
                catch(SocketException)
                {
                    Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    Thread.Sleep(2000);
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }

        public void AddMicroService(RegisterServiceInfo service)
        {
            if (_refereeAddress == null)
                return;
            using (NetClient client = new NetClient(_refereeAddress))
            {
                client.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.RegisterSerivce,
                    Content = new RegisterServiceLocation { 
                        Host = service.Host,
                        Port = service.Port
                    }.ToJsonString()
                });
                var cmd = client.ReadServiceObject<InvokeResult>();
                if (cmd.Success == false)
                    throw new Exception("not master");
            }
        }

        public void RemoveMicroService(RegisterServiceInfo service)
        {
            if (_refereeAddress == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    using (NetClient client = new NetClient(_refereeAddress.Address, _refereeAddress.Port))
                    {
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = CommandType.UnRegisterSerivce,
                            Content = new RegisterServiceLocation
                            {
                                Host = service.Host,
                                Port = service.Port
                            }.ToJsonString()
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
