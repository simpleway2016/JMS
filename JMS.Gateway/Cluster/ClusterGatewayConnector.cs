
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
using JMS.Applications;

namespace JMS.Cluster
{
    public class ClusterGatewayConnector
    {
        IConfiguration _configuration;
        IRegisterServiceManager _registerServiceManager;
        NetAddress _otherGatewayAddress;
        ILogger<ClusterGatewayConnector> _logger;
        LockKeyManager _lockKeyManager;
        ConcurrentDictionary<string, RegisterServiceLocation> _waitServiceList;
        Gateway _gateway;
        ConcurrentQueue<(int action, KeyObject keyObject)> _keyChangeQueue = new ConcurrentQueue<(int action, KeyObject keyObject)>();
        AutoResetEvent _keyChangeQueueEvent = new AutoResetEvent(false);

        /// <summary>
        /// 记录当前网关是否是master
        /// </summary>
        public bool IsMaster { get; private set; }
        public ClusterGatewayConnector(IConfiguration configuration,
            LockKeyManager lockKeyManager,
            Gateway gateway,
            IRegisterServiceManager registerServiceManager,
            TransactionStatusManager transactionStatusManager,
            ILogger<ClusterGatewayConnector> logger)
        {
            _configuration = configuration;
            _registerServiceManager = registerServiceManager;
            _otherGatewayAddress = configuration.GetSection("Cluster:Gateway").Get<NetAddress>();
            _logger = logger;
            _lockKeyManager = lockKeyManager;
            _gateway = gateway;

            if (_otherGatewayAddress != null)
            {
                IsMaster = configuration.GetSection("Cluster:IsMaster").Get<bool>();
                transactionStatusManager.TransactionSuccess += OnTransactionSuccess;
                transactionStatusManager.TransactionRemove += OnTransactionRemove;

                _lockKeyManager.LockKey += _lockKeyManager_LockKey;
                _lockKeyManager.UnlockKey += _lockKeyManager_UnlockKey;

                new Thread(checkKeyChangeQueue).Start();
            }
            else
            {
                IsMaster = true;
            }
        }

        private void _lockKeyManager_UnlockKey(object sender, KeyObject e)
        {
            _keyChangeQueue.Enqueue((2, e));
            _keyChangeQueueEvent.Set();
        }

        private void _lockKeyManager_LockKey(object sender, KeyObject e)
        {
            _keyChangeQueue.Enqueue((1, e));
            _keyChangeQueueEvent.Set();
        }

        /// <summary>
        /// 将lockkey的变化同步到其他网关
        /// </summary>
        void checkKeyChangeQueue()
        {
            var wait = true;
            while (true)
            {
                if (wait)
                {
                    _keyChangeQueueEvent.WaitOne();
                }
                try
                {
                    if (_keyChangeQueue.Count > 0)
                    {
                        using (NetClient client = new CertClient())
                        {
                            client.Connect(_otherGatewayAddress);
                            while (_keyChangeQueue.TryDequeue(out (int action, KeyObject keyObject) item))
                            {
                                _logger.LogDebug($"同步{(item.action == 1 ? "add" : "remove")} {item.keyObject.Key}到其他网关");


                                if (item.action == 1)
                                {
                                    client.WriteServiceData(new GatewayCommand
                                    {
                                        Type = (int)CommandType.AddLockKey,
                                        Content = item.keyObject.ToJsonString()
                                    });
                                }
                                else
                                {
                                    client.WriteServiceData(new GatewayCommand
                                    {
                                        Type = (int)CommandType.RemoveLockKey,
                                        Content = item.keyObject.Key
                                    });
                                }

                            }
                            client.WriteServiceData(new GatewayCommand
                            {
                                Type = (int)CommandType.HealthyCheck
                            });
                            try
                            {
                                client.ReadServiceObject<InvokeResult>();
                            }
                            catch
                            {
                            }
                        }
                    }
                    wait = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                    Thread.Sleep(3000);
                    wait = false;
                }
            }
        }

        /// <summary>
        /// 申请成为master
        /// </summary>
        internal void BeMaster(int tryCount = 3)
        {
            if (_otherGatewayAddress != null)
            {
                Task.Run(() =>
                {
                    for (int i = 0; i < tryCount; i++)
                    {
                        try
                        {
                            using (NetClient client = new CertClient())
                            {
                                client.Connect(_otherGatewayAddress);
                                client.WriteServiceData(new GatewayCommand
                                {
                                    Type = (int)CommandType.BeGatewayMaster,
                                    Content = new BeMasterContent { Id = _gateway.Id, IsMaster = IsMaster }.ToJsonString()
                                });
                                var cmd = client.ReadServiceObject<InvokeResult>();
                                if (cmd.Success)
                                {
                                    _logger.LogInformation($"网关{_otherGatewayAddress}同意我成为主网关");
                                    if (IsMaster == false)
                                    {
                                        //在没成为主网关前，将所有lockey设置一个有效期，成为主网关，等微服务提交所有lock key，会取消这个有效期
                                        _lockKeyManager.ResetAllKeyExpireTime();
                                        IsMaster = true;
                                    }
                                    uploadLockKeysToOtherGateway();
                                }
                                else
                                {
                                    _logger.LogInformation($"网关{_otherGatewayAddress}拒绝我成为主网关");

                                    IsMaster = false;

                                    //不是主网关，需要断开所有微服务
                                    _registerServiceManager.DisconnectAllServices();

                                    //上传所有lock key
                                    uploadLockKeysToOtherGateway();

                                    keepAliveWithMaster();
                                }
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            //_logger.LogError(ex, "");
                        }
                    }

                    if (IsMaster == false)
                    {
                        _logger.LogInformation($"与网关{_otherGatewayAddress}连接失败，我自己成为master");

                        //在没成为主网关前，将所有lockey设置一个有效期，成为主网关，等微服务提交所有lock key，会取消这个有效期
                        _lockKeyManager.ResetAllKeyExpireTime();

                        //3次网络访问失败，自己当master
                        IsMaster = true;
                    }
                    Thread.Sleep(3000);
                    BeMaster(1);
                });
            }
        }

        NetClient CreateOtherGatewayClient()
        {
            var client = new CertClient();
            client.Connect(_otherGatewayAddress);
            return client;
        }

        void uploadLockKeysToOtherGateway()
        {
            //上传已经lock的key
            var keys = _lockKeyManager.GetAllKeys();
            if (keys.Length > 0)
            {
                foreach (var key in keys)
                {
                    _lockKeyManager_LockKey(null, key);
                }
            }
        }

        void keepAliveWithMaster()
        {
            try
            {
                //连上主网关，直到连接出现问题，再申请成为主网关
                using (NetClient client = CreateOtherGatewayClient())
                {
                    _logger?.LogInformation("与主网关连接心跳");
                    client.KeepHeartBeating();
                    Thread.Sleep(100);
                    _logger?.LogInformation("与主网关连接断开");
                }
                BeMaster(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
                BeMaster(1);
            }
        }

        internal void SomeoneWantToBeMaster(NetClient netclient, GatewayCommand cmd)
        {
            BeMasterContent beMasterContent = cmd.Content.FromJson<BeMasterContent>();
            if (IsMaster && beMasterContent.IsMaster == false)
            {
                _logger.LogInformation($"拒绝{beMasterContent.Id}成为master");
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
                return;
            }
            else if (IsMaster == false && beMasterContent.IsMaster)
            {
                _logger.LogInformation($"同意{beMasterContent.Id}成为master");

                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true
                });
                return;
            }


            var ret = string.Compare(_gateway.Id, beMasterContent.Id);
            if (ret > 0)
            {
                _logger.LogInformation($"同意{beMasterContent.Id}成为master");

                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true
                });
            }
            else
            {
                _logger.LogInformation($"拒绝{beMasterContent.Id}成为master");

                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
            }
        }

        private void OnTransactionRemove(object sender, string tranid)
        {
            if (_otherGatewayAddress == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    using (NetClient client = CreateOtherGatewayClient())
                    {
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.RemoveTransactionStatus,
                            Content = tranid
                        });
                        var cmd = client.ReadServiceObject<InvokeResult>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                }
            });
        }

        private void OnTransactionSuccess(object sender, string tranid)
        {
            if (_otherGatewayAddress == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    using (NetClient client = CreateOtherGatewayClient())
                    {
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.ReportTransactionStatus,
                            Content = tranid
                        });
                        var cmd = client.ReadServiceObject<InvokeResult>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                }
            });
        }


    }

    class BeMasterContent
    {
        public string Id { get; set; }
        public bool IsMaster { get; set; }
    }
}
