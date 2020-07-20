using JMS.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace JMS
{
    public class NetClientPool
    {
        const int POOLSIZE = 20;
        const int RELEASESECONDS = 5;
        static ConcurrentDictionary<string, NetClientSeat[]> Dict = new ConcurrentDictionary<string, NetClientSeat[]>();
        static NetClientPool()
        {
            new Thread(checkTime).Start();
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
                            if (item.Client != null && item.Used == 0 && (DateTime.Now - item.OnSeatTime).TotalSeconds >= RELEASESECONDS)
                            {
                                if (Interlocked.CompareExchange(ref item.Used, 1, 0) == 0)
                                {
                                    item.Client?.Dispose();
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
                Thread.Sleep(2000);
            }
        }

        public static NetClient CreateClient( NetAddress proxy, string ip,int port, X509Certificate2 cert)
        {
            return CreateClient(proxy, new NetAddress(ip, port), cert);
        }
        public static NetClient CreateClient(NetAddress proxy, NetAddress addr, X509Certificate2 cert)
        {
            var key = $"{addr.Address}--{addr.Port}";
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
            var key = $"{client.Address}--{client.Port}";
            NetClientSeat[] array;
            if (Dict.TryGetValue(key, out array) == false)
            {
                Dict.TryAdd(key, GetArray());
                array = Dict[key];
            }

            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];
                if (item.Client == null && item.Used == 0)
                {
                    if (Interlocked.CompareExchange(ref item.Used, 1, 0) == 0)
                    {
                        item.OnSeatTime = DateTime.Now;
                        item.Client = client;
                        client.ReadTimeout = 16000;
                        item.Used = 0;
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

        static NetClient GetFree(NetClientSeat[] source)
        {
            for(int i = 0; i < source.Length; i ++)
            {
                var item = source[i];
                if(item.Client != null && item.Used == 0)
                {
                    if( Interlocked.CompareExchange(ref item.Used , 1 , 0) == 0)
                    {
                        var ret = item.Client;
                        item.Client = null;
                        item.Used = 0;
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
        public int Used;
        public DateTime OnSeatTime;
    }
}
