using JMS;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>
    /// 微服务事务管理
    /// </summary>
    public class MicroServiceTransaction : IDisposable
    {
        List<Client> _Clients = new List<Client>();
        List<Task> _Tasks = new List<Task>();

        private string _TransactionId;
        public string TransactionId
        {
            get => _TransactionId;
            set
            {
                if (_TransactionId != value)
                {
                    _TransactionId = value;
                    this.Header["TranId"] = value;
                }
            }
        }
        bool _finished = false;
        public string GatewayAddress { get; }
        public int GatewayPort { get; }
        public Dictionary<string, string> Header = new Dictionary<string, string>();
        public MicroServiceTransaction(string gatewayAddress, int port)
        {
            GatewayAddress = gatewayAddress;
            GatewayPort = port;
        }
        public RegisterServiceRunningInfo[] ListMicroService(string serviceName)
        {
            using (var netclient = new Way.Lib.NetStream(GatewayAddress, GatewayPort))
            {
#if DEBUG
                netclient.ReadTimeout = 0;
#else
                netclient.ReadTimeout = 20000;
#endif
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetAllServiceProviders,
                    Content = serviceName,
                    Header = this.Header
                });
                var serviceLocations = netclient.ReadServiceObject<RegisterServiceRunningInfo[]>();
               
                return serviceLocations;
            }
        }
        public IMicroService GetMicroService( string serviceName)
        {
            var invoker = new Invoker(this, serviceName);
            if (invoker.Init())
                return invoker;
            return null;
        }

        internal void AddClient(Client   client)
        {
            lock(_Clients)
            {
                _Clients.Add(client);
            }           
        }
        internal void AddTask(Task task)
        {
            lock (_Tasks)
            {
                _Tasks.Add(task);
            }
        }

        void waitTasks()
        {
            Task.WaitAll(_Tasks.ToArray());
        }

        public void Commit()
        {
            if (_finished)
                return;
            _finished = true;
            var errors = endResponse(InvokeType.CommitTranaction);
            if (errors.Count > 0)
                throw new TransactionCommitArrayException(errors, "commit transaction error");
        }

        List<TransactionCommitException> endResponse(InvokeType invokeType)
        {
            waitTasks();

            List<TransactionCommitException> errors = new List<TransactionCommitException>(_Clients.Count);

            Parallel.For(0, _Clients.Count, (i) => {
                var client = _Clients[i];
                bool reconnect = false;
                while (true)
                {
                    try
                    {
                        if(reconnect)
                        {
                            Thread.Sleep(1000);
                            client.ReConnect();
                        }
                       
                        if (errors.Count == 0)
                        {
                            client.NetClient.WriteServiceData(new InvokeCommand()
                            {
                                Type = invokeType,
                                Header = this.Header
                            });
                            client.NetClient.ReadServiceObject<InvokeResult>();
                        }
                        else
                        {
                            errors.Add(new TransactionCommitException(client.ServiceLocation, "cancel"));
                        }
                        break;
                    }
                    catch(SocketException ex)
                    {
                        if (client.ReConnectCount < 10)
                        {
                            client.NetClient.Dispose();
                            reconnect = true;
                        }
                        else
                        {
                            errors.Add(new TransactionCommitException(client.ServiceLocation, ex.Message));
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new TransactionCommitException(client.ServiceLocation, ex.Message));
                        break;
                    }
                }
                client.NetClient.Dispose();
            });

            _Clients.Clear();
            _Tasks.Clear();

            return errors;
           
        }

        public void Rollback()
        {
            if (_finished)
                return;
            _finished = true;

           var errors = endResponse(InvokeType.RollbackTranaction);
            if (errors.Count > 0)
                throw new TransactionCommitArrayException(errors, "rollback transaction error");
        }

        public void Dispose()
        {
            Rollback();
        }
    }
}
