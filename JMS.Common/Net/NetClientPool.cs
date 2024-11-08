using JMS.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    public class NetClientPool
    {
        internal const int RELEASESECONDS = 30;
        internal static int DefaultReadTimeout = 16000;
        static ConcurrentDictionary<(string, int), NetClientSeatCollection> TargetSeatGroups = new ConcurrentDictionary<(string, int), NetClientSeatCollection>();

        public static event EventHandler<NetClient> CreatedNewClient;


        [Obsolete]
        public static void SetConnectionPoolSize(int size)
        {
           
        }

        [Obsolete]
        public static int GetConnectionPoolSize()
        {
            return 0;
        }

        /// <summary>
        /// 设置默认超时时间，单位：毫秒，默认:16000
        /// </summary>
        public static void SetDefaultReadTimeout(int timeout)
        {
            if (timeout < 0)
                throw new ArgumentException("invalid timeout");
            DefaultReadTimeout = timeout;
        }


        public static NetClient CreateClient(NetAddress proxy, string ip, int port, Action<NetClient> newClientCallback = null)
        {
            return CreateClient(proxy, new NetAddress(ip, port), newClientCallback);
        }
        public static Task<NetClient> CreateClientAsync(NetAddress proxy, string ip, int port, Func<NetClient, Task> newClientCallback = null)
        {
            return CreateClientAsync(proxy, new NetAddress(ip, port), newClientCallback);
        }
        public static NetClient CreateClient(NetAddress proxy, NetAddress addr, Action<NetClient> newClientCallback = null)
        {
            var key = (addr.Address, addr.Port);

            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());

            var freeitem = seatGroup.GetFree();
            if (freeitem == null)
            {
                freeitem = new ProxyClient(proxy);
                freeitem.Connect(addr);
                freeitem.KeepAlive = true;
                newClientCallback?.Invoke(freeitem);
                CreatedNewClient?.Invoke(null, freeitem);
            }

            return freeitem;
        }

        public static async Task<NetClient> CreateClientAsync(NetAddress proxy, NetAddress addr, Func<NetClient, Task> newClientCallback = null)
        {
            var key = (addr.Address, addr.Port);

            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());

            var freeitem = seatGroup.GetFree();
            if (freeitem == null)
            {
                freeitem = new ProxyClient(proxy);
                await freeitem.ConnectAsync(addr);
                freeitem.KeepAlive = true;
                if (newClientCallback != null)
                {
                    await newClientCallback(freeitem);
                }
                CreatedNewClient?.Invoke(null, freeitem);
            }

            return freeitem;
        }

        public static async Task<NetClient> CreateClientByKeyAsync(string key, Func<NetClient, Task> newClientCallback = null)
        {
            var keyObj = (key, 0);
            var seatGroup = TargetSeatGroups.GetOrAdd(keyObj, k => new NetClientSeatCollection());

            var freeitem = seatGroup.GetFree();
           
            return freeitem;
        }
        public static void AddClientToPool(NetClient client)
        {
            if (client == null)
                return;

            if (client.HasSocketException)
            {
                client.Dispose();
                return;
            }
            if (!client.KeepAlive)
            {
                client.Dispose();
                return;
            }
            var key = (client.NetAddress.Address, client.NetAddress.Port);
            var seatGroup = TargetSeatGroups.GetOrAdd(key, k => new NetClientSeatCollection());


            seatGroup.AddClient(client);
        }
        public static void AddClientToPoolByKey(NetClient client,string key)
        {
            if (client == null)
                return;

            if (client.HasSocketException)
            {
                client.Dispose();
                return;
            }
            if (!client.KeepAlive)
            {
                client.Dispose();
                return;
            }
            var keyObj = (key, 0);
            var seatGroup = TargetSeatGroups.GetOrAdd(keyObj, k => new NetClientSeatCollection());

            seatGroup.AddClient( client);
        }


        public static int GetPoolAliveCount(NetAddress addr)
        {
            var key = (addr.Address, addr.Port);

            if (TargetSeatGroups.TryGetValue(key, out NetClientSeatCollection seatGroup))
            {
                return seatGroup.Connects.Count;
            }
            return 0;
        }

      
    }

    class NetClientSeatCollection
    {
        public ConcurrentQueue<NetClientSeat> Connects;
        public bool IsReady;
        int _state = 0;//0=未初始化 1=准备初始化 2=初始化完毕

        void init()
        {
            if (_state == 2)
                return;
            if( Interlocked.CompareExchange(ref _state , 1 , 0) == 0)
            {
                Connects = new ConcurrentQueue<NetClientSeat>();
                _state = 2;
                IsReady = true;
            }
            else
            {
                while (_state != 2)
                    Thread.Sleep(10);
            }
        }

       
        public NetClient GetFree()
        {
            if (!IsReady)
            {
                init();
            }

            while (true)
            {
                if(Connects.TryDequeue(out NetClientSeat seat))
                {
                    if (seat.Client.Socket == null)
                        continue;
                    else if((DateTime.Now - seat.OnSeatTime).TotalSeconds >= NetClientPool.RELEASESECONDS){
                        seat.Client.Dispose();
                        continue;
                    }
                    return seat.Client;
                }
                else
                {
                    return null;
                }
            }
            
        }

        public void AddClient(  NetClient client)
        {
            if (!IsReady)
            {
                init();
            }

            Connects.Enqueue(new NetClientSeat(client));
            //连接断开后自动释放socket
            client.checkStatus();
        }

    }

    class NetClientSeat
    {
        public NetClient Client;
        public DateTime OnSeatTime;
        public NetClientSeat(NetClient client)
        {
            this.Client = client;
            OnSeatTime = DateTime.Now;
        }
    }
}
