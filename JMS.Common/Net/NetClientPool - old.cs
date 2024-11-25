//using JMS.Common;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net.Sockets;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace JMS
//{
//    public class NetClientPool
//    {
//        internal static int POOLSIZE = 20;
//        const int RELEASESECONDS = 20;
//        internal static int DefaultReadTimeout = 16000;
//        static ConcurrentDictionary<(string, int), NetClientSeatCollection> TargetSeatGroups = new ConcurrentDictionary<(string, int), NetClientSeatCollection>();
//        static NetClientPool()
//        {
//            POOLSIZE = Math.Max(20, Environment.ProcessorCount * 10);
//            new Thread(checkTime).Start();

//        }

//        public static event EventHandler<NetClient> CreatedNewClient;

//        /// <summary>
//        /// 设置连接池大小（默认CPU线程数x10）
//        /// </summary>
//        /// <param name="size"></param>
//        public static void SetConnectionPoolSize(int size)
//        {
//            if (size < 0)
//                throw new Exception("参数错误");
//            POOLSIZE = size;
//        }

//        public static int GetConnectionPoolSize()
//        {
//            return POOLSIZE;
//        }

//        /// <summary>
//        /// 设置默认超时时间，单位：毫秒，默认:16000
//        /// </summary>
//        public static void SetDefaultReadTimeout(int timeout)
//        {
//            if (timeout < 0)
//                throw new ArgumentException("invalid timeout");
//            DefaultReadTimeout = timeout;
//        }

//        static void checkTime()
//        {
//            while (true)
//            {
//                try
//                {
//                    foreach( var pair in TargetSeatGroups)
//                    {
//                        if (pair.Value.IsReady)
//                        {
//                            foreach (var item in pair.Value.Connects)
//                            {
//                                if (item.Used == 2 && ((DateTime.Now - item.OnSeatTime).TotalSeconds >= RELEASESECONDS || item.Client.Socket == null))
//                                {
//                                    if (Interlocked.CompareExchange(ref item.Used, 3, 2) == 2)
//                                    {
//                                        item.Client.Dispose();
//                                        item.Client = null;
//                                        item.Used = 0;
//                                    }
//                                }
//                            }
//                        }
//                    }

//                }
//                catch
//                {

//                }
//                Thread.Sleep(3000);
//            }
//        }

//        public static NetClient CreateClient(NetAddress proxy, string ip, int port, Action<NetClient> newClientCallback = null)
//        {
//            return CreateClient(proxy, new NetAddress(ip, port), newClientCallback);
//        }
//        public static Task<NetClient> CreateClientAsync(NetAddress proxy, string ip, int port, Func<NetClient, Task> newClientCallback = null)
//        {
//            return CreateClientAsync(proxy, new NetAddress(ip, port), newClientCallback);
//        }
//        public static NetClient CreateClient(NetAddress proxy, NetAddress addr, Action<NetClient> newClientCallback = null)
//        {
//            var key = (addr.Address, addr.Port);

//            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());

//            var freeitem = seatGroup.GetFree();
//            if (freeitem == null)
//            {
//                freeitem = new ProxyClient(proxy);
//                freeitem.Connect(addr);
//                freeitem.KeepAlive = true;
//                newClientCallback?.Invoke(freeitem);
//                CreatedNewClient?.Invoke(null, freeitem);
//            }

//            return freeitem;
//        }

//        public static async Task<NetClient> CreateClientAsync(NetAddress proxy, NetAddress addr, Func<NetClient, Task> newClientCallback = null)
//        {
//            var key = (addr.Address, addr.Port);

//            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());

//            var freeitem = seatGroup.GetFree();
//            if (freeitem == null)
//            {
//                freeitem = new ProxyClient(proxy);
//                await freeitem.ConnectAsync(addr);
//                freeitem.KeepAlive = true;
//                if (newClientCallback != null)
//                {
//                    await newClientCallback(freeitem);
//                }
//                CreatedNewClient?.Invoke(null, freeitem);
//            }

//            return freeitem;
//        }

//        public static async Task<NetClient> CreateClientByKeyAsync(string key, Func<NetClient, Task> newClientCallback = null)
//        {
//            var keyObj = (key, 0);
//            var seatGroup = TargetSeatGroups.GetOrAdd(keyObj, k => new NetClientSeatCollection());

//            var freeitem = seatGroup.GetFree();
           
//            return freeitem;
//        }
//        public static void AddClientToPool(NetClient client)
//        {
//            if (client == null)
//                return;

//            if (client.HasSocketException)
//            {
//                client.Dispose();
//                return;
//            }
//            if (!client.KeepAlive)
//            {
//                client.Dispose();
//                return;
//            }
//            var key = (client.NetAddress.Address, client.NetAddress.Port);
//            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());


//            seatGroup.AddClient(client);
//        }
//        public static void AddClientToPoolByKey(NetClient client,string key)
//        {
//            if (client == null)
//                return;

//            if (client.HasSocketException)
//            {
//                client.Dispose();
//                return;
//            }
//            if (!client.KeepAlive)
//            {
//                client.Dispose();
//                return;
//            }
//            var keyObj = (key, 0);
//            var seatGroup = TargetSeatGroups.GetOrAdd(keyObj, k => new NetClientSeatCollection());

//            seatGroup.AddClient( client);
//        }


//        public static int GetPoolAliveCount(NetAddress addr)
//        {
//            var key = (addr.Address, addr.Port);

//            if (TargetSeatGroups.TryGetValue(key, out NetClientSeatCollection seatGroup))
//            {
//                int count = 0;
//                for (int i = 0; i < seatGroup.Connects.Length; i++)
//                {
//                    var item = seatGroup.Connects[i];
//                    if (item.Client != null)
//                    {
//                        count++;
//                    }
//                }
//                return count;
//            }
//            return 0;
//        }

      
//    }

//    class NetClientSeatCollection
//    {
//        public NetClientSeat[] Connects;
//        public bool IsReady;
//        int _state = 0;//0=未初始化 1=准备初始化 2=初始化完毕

//        void init()
//        {
//            if (_state == 2)
//                return;
//            if( Interlocked.CompareExchange(ref _state , 1 , 0) == 0)
//            {
//                Connects = new NetClientSeat[NetClientPool.POOLSIZE];
//                for (int i = 0; i < Connects.Length; i++)
//                {
//                    Connects[i] = new NetClientSeat();
//                }
//                _state = 2;
//                IsReady = true;
//            }
//            else
//            {
//                while (_state != 2)
//                    Thread.Sleep(10);
//            }
//        }

       
//        public NetClient GetFree()
//        {
//            if (!IsReady)
//            {
//                init();
//            }

//            for (int i = 0; i < this.Connects.Length; i++)
//            {
//                var item = this.Connects[i];
//                if (item.Used == 2)
//                {
//                    if (Interlocked.CompareExchange(ref item.Used, 3, 2) == 2)
//                    {
//                        var ret = item.Client;
//                        item.Client = null;
//                        item.Used = 0;

//                        if(ret.Socket == null)
//                        {
//#if DEBUG
//                            System.Diagnostics.Debug.WriteLine("有一个连接已经失效");
//#endif
//                            //此连接已经断开
//                            continue;
//                        }

//                        ret.ReadTimeout = NetClientPool.DefaultReadTimeout;
//                        return ret;
//                    }
//                }
//            }
//            return null;
//        }

//        public void AddClient(  NetClient client)
//        {
//            if (!IsReady)
//            {
//                init();
//            }

//            for (int i = 0; i < this.Connects.Length; i++)
//            {
//                var item = this.Connects[i];
               
//                if (item.Used == 0)
//                {
//                    if (Interlocked.CompareExchange(ref item.Used, 1, 0) == 0)
//                    {
//                        item.OnSeatTime = DateTime.Now;
//                        item.Client = client;
//                        client.ReadTimeout = 0;
//                        item.Used = 2;

//                        //连接断开后自动释放socket
//                        client.checkStatus();
//                        return;
//                    }
//                }
//            }

//            //没有位置，这个释放掉
//            client.Dispose();
//        }

//    }

//    class NetClientSeat
//    {
//        public NetClient Client;
//        /// <summary>
//        /// 0 空闲位置
//        /// 1 将要放入
//        /// 2 已经放入
//        /// 3 准备取出
//        /// </summary>
//        public int Used;
//        public DateTime OnSeatTime;
//    }
//}
