using JMS.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace JMS
{
    public class NetClientPool
    {
        static int POOLSIZE = 5000;
        const int RELEASESECONDS = 10;
        static ConcurrentDictionary<(string,int), NetClientSeat[]> Dict = new ConcurrentDictionary<(string, int), NetClientSeat[]>();
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
            while(true)
            {
                try
                {
                    var keys = Dict.Keys.ToArray();
                    foreach( var key in keys )
                    {
                        var array = Dict[key];
                        for (int i = 0; i < array.Length; i++)
                        {
                            var item = array[i];
                            if ( item.Used == 2 && (DateTime.Now - item.OnSeatTime).TotalSeconds >= RELEASESECONDS)
                            {
                                if (Interlocked.CompareExchange(ref item.Used, 3, 2) == 2)
                                {
                                    item.Client.Dispose();
                                    item.Client = null;
                                    item.Used = 0;
                                }
                            }
                        }
                    }
                }
                catch
                {

                }
                Thread.Sleep(5000);
            }
        }

        public static NetClient CreateClient( NetAddress proxy, string ip,int port, X509Certificate2 cert,Action<NetClient> newClientCallback = null)
        {
            return CreateClient(proxy, new NetAddress(ip, port), cert,newClientCallback);
        }
        public static NetClient CreateClient(NetAddress proxy, NetAddress addr, X509Certificate2 cert, Action<NetClient> newClientCallback = null)
        {
            var key = (addr.Address,addr.Port);
            NetClientSeat[] array;
            if (Dict.TryGetValue(key, out array) == false)
            {
                Dict.TryAdd(key, GetArray());
                array = Dict[key];
            }

            var freeitem = GetFree(array);
            if (freeitem == null)
            {
                freeitem = new ProxyClient(proxy, addr, cert);
                freeitem.KeepAlive = array.Any(m => m.Client == null);
                newClientCallback?.Invoke(freeitem);
                CreatedNewClient?.Invoke(null, freeitem);
            }

            return freeitem;
        }
        public static void AddClientToPool(NetClient client)
        {
            if (client == null)
                return;

            if(client.HasSocketException)
            {
                client.Dispose();
                return;
            }
            if (!client.KeepAlive)
            {
                client.Dispose();
                return;
            }
            var key = (client.Address , client.Port);
            NetClientSeat[] array;
            if (Dict.TryGetValue(key, out array) == false)
            {
                Dict.TryAdd(key, GetArray());
                array = Dict[key];
            }

            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];
                if (item.Used == 0)
                {
                    if (Interlocked.CompareExchange(ref item.Used, 1, 0) == 0)
                    {
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

        static NetClientSeat[] GetArray()
        {
            var ret = new NetClientSeat[POOLSIZE];
            for(int i = 0; i < ret.Length; i ++)
            {
                ret[i] = new NetClientSeat();
            }
            return ret;
        }

        public static int GetPoolAliveCount(NetAddress addr)
        {
            var key = (addr.Address, addr.Port);
            if (Dict.ContainsKey(key) == false)
                return 0;
            var queue = Dict[key];
            int count = 0;
            for (int i = 0; i < queue.Length; i++)
            {
                var item = queue[i];
                if (item.Client != null )
                {
                    count++;
                }
            }
            return count;
        }

        static NetClient GetFree(NetClientSeat[] source)
        {
            for(int i = 0; i < source.Length; i ++)
            {
                var item = source[i];
                if(item.Used == 2)
                {
                    if( Interlocked.CompareExchange(ref item.Used , 3 , 2) == 2)
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
    }

    class NetClientSeat
    {
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
