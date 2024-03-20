using JMS.Dtos;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace JMS.TransactionReporters
{
    /// <summary>
    /// 向网关报告事务情况
    /// </summary>
    internal class GatewayReporter : ITransactionReporter
    {
      

        public void ReportTransactionSuccess(RemoteClient remoteClient, string tranid)
        {
            var netclient = NetClientPool.CreateClient(remoteClient.ProxyAddress, remoteClient.GatewayAddress);
            netclient.ReadTimeout = remoteClient.Timeout;
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = (int)CommandType.ReportTransactionStatus,
                    Content = tranid
                });
                byte[] data = ArrayPool<byte>.Shared.Rent(4);
                try
                {
                    int readed = netclient.Socket.Receive(data, 4, SocketFlags.Peek);
                    if (readed == 0)
                    {
                        //网关不支持此命令
                        netclient.Dispose();
                        return;
                    }
                    else
                    {
                        netclient.ReadServiceObject<InvokeResult>();
                    }
                    NetClientPool.AddClientToPool(netclient);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(data);
                }
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }
        }


        public void ReportTransactionCompleted(RemoteClient remoteClient, string tranid)
        {
            var netclient = NetClientPool.CreateClient(remoteClient.ProxyAddress, remoteClient.GatewayAddress);
            netclient.ReadTimeout = remoteClient.Timeout;
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = (int)CommandType.RemoveTransactionStatus,
                    Content = tranid
                });
                byte[] data = ArrayPool<byte>.Shared.Rent(4);
                try
                {
                    int readed = netclient.Socket.Receive(data, 4, SocketFlags.Peek);
                    if (readed == 0)
                    {
                        //网关不支持此命令
                        netclient.Dispose();
                        return;
                    }
                    else
                    {
                        netclient.ReadServiceObject<InvokeResult>();
                    }
                    NetClientPool.AddClientToPool(netclient);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(data);
                }
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }
        }
    }
}
