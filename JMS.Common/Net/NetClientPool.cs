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
        internal static int POOLSIZE = 65535;
        const int RELEASESECONDS = 10;
        static NetClientSeatCollection SeatCollection = new NetClientSeatCollection();
        static NetClientPool()
        {
            new Thread(checkTime).Start();

        }

        public static event EventHandler<NetClient> CreatedNewClient;

        /// <summary>
        /// 设置连接池大小（默认为50000）
        /// </summary>
        /// <param name="size"></param>
        public static void SetConnectionPoolSize(int size)
        {
            if (size < 0)
                throw new Exception("参数错误");
            POOLSIZE = size;
        }

        static void checkTime()
        {
            while (true)
            {
                try
                {
                    for (int i = 0; i < SeatCollection.Connects.Length; i++)
                    {
                        var item = SeatCollection.Connects[i];
                        if (item.Used == 2 && (DateTime.Now - item.OnSeatTime).TotalSeconds >= RELEASESECONDS)
                        {
                            if (Interlocked.CompareExchange(ref item.Used, 3, 2) == 2)
                            {
                                item.Client.Dispose();
                                item.Client = null;
                                item.Used = 0;
                            }
                        }
                    }

                    for (int i = SeatCollection.Connects.Length - 1; i >= 0; i--)
                    {
                        var item = SeatCollection.Connects[i];
                        if (item.Used != 0)
                        {
                            SeatCollection.MaxIndex = i + 10;
                            break;
                        }
                    }
                }
                catch
                {

                }
                Thread.Sleep(5000);
            }
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

            var freeitem = SeatCollection.GetFree(key);
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

            var freeitem = SeatCollection.GetFree(key);
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

            SeatCollection.AddClient(key , client);
        }


        public static int GetPoolAliveCount(NetAddress addr)
        {
            var key = (addr.Address, addr.Port);

            int count = 0;
            for (int i = 0; i < SeatCollection.Connects.Length; i++)
            {
                var item = SeatCollection.Connects[i];
                if ( item.Key == key && item.Client != null)
                {
                    count++;
                }
            }
            return count;
        }

      
    }

    class NetClientSeatCollection
    {
        public NetClientSeat[] Connects;
        public int MaxIndex;
        public NetClientSeatCollection()
        {
            Connects = new NetClientSeat[NetClientPool.POOLSIZE];
            for (int i = 0; i < Connects.Length; i++)
            {
                Connects[i] = new NetClientSeat();
            }
        }

       
        public NetClient GetFree((string,int) key)
        {
            for (int i = 0; i < this.Connects.Length && i < this.MaxIndex; i++)
            {
                var item = this.Connects[i];
                if (item.Used == 2 && item.Key == key)
                {
                    if (Interlocked.CompareExchange(ref item.Used, 3, 2) == 2)
                    {
                        var ret = item.Client;
                        item.Client = null;
                        item.Used = 0;
                        try
                        {
                            ret.Socket.Send(new byte[0]);
                        }
                        catch (SocketException)
                        {
                            //连接已断开
                            ret.Dispose();
                            continue;
                        }
                        return ret;
                    }
                }
            }
            return null;
        }

        public void AddClient( (string,int) key, NetClient client)
        {
            for (int i = 0; i < this.Connects.Length; i++)
            {
                var item = this.Connects[i];
               
                if (item.Used == 0)
                {
                    if (this.MaxIndex < i + 10)
                        this.MaxIndex = i + 10;

                    if (Interlocked.CompareExchange(ref item.Used, 1, 0) == 0)
                    {
                        item.Key = key;
                        item.OnSeatTime = DateTime.Now;
                        item.Client = client;
                        client.ReadTimeout = 16000;
                        item.Used = 2;
                        return;
                    }
                }
            }

            //没有位置，这个释放掉
            client.Dispose();
        }

    }

    class NetClientSeat
    {
        public (string, int) Key;
        public NetClient Client;
        /// <summary>
        /// 0 空闲位置
        /// 1 将要放入
        /// 2 已经放入
        /// 3 准备取出
        /// </summary>
        public int Used;
        public DateTime OnSeatTime;
    }
}
