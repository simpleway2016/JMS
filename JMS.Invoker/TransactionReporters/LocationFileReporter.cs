using JMS.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;
using Microsoft.Extensions.Logging;
using JMS.InvokeConnects;

namespace JMS.TransactionReporters
{
    /// <summary>
    /// 用本地文件记录事务情况
    /// </summary>
    internal class LocationFileReporter : ITransactionReporter
    {
        static string SaveFolder = "./$$_JMS.Invoker.Transactions";

        public LocationFileReporter()
        {
            if (Directory.Exists(SaveFolder) == false)
                Directory.CreateDirectory(SaveFolder);

            new Thread(retryExecTrancations).Start();
        }

        /// <summary>
        /// 重新执行事务
        /// </summary>
        void retryExecTrancations()
        {
            while (true)
            {
                try
                {
                    var files = Directory.GetFiles(SaveFolder, "*.json");
                    foreach (var file in files)
                    {
                        var fileinfo = new FileInfo(file);
                        if ((DateTime.Now - fileinfo.CreationTime).TotalSeconds < 30)
                            continue;
                        retryTranaction(file, File.ReadAllText(file, Encoding.UTF8), Path.GetFileNameWithoutExtension(file));
                    }
                }
                catch (Exception)
                {

                }
                Thread.Sleep(10000);
            }
        }

        void retryTranaction(string filepath, string filecontent, string tranId)
        {
            try
            {
                TransactionReporterRoute.Logger?.LogInformation($"重新执行事务{tranId}");
                ConcurrentQueue<RegisterServiceLocation> failds = new ConcurrentQueue<RegisterServiceLocation>();

                var obj = filecontent.FromJson<FileReportContent>();
                Parallel.ForEach(obj.Locations, location =>
                {
                    try
                    {
                        using (var invokeConnect = InvokeConnectFactory.Create(null, location, null))
                        {
                            invokeConnect.RetryTranaction(obj.ProxyAddress, location, obj.Cert, tranId);
                        }

                    }
                    catch(Exception ex)
                    {
                        location.Description = ex.ToString();
                        failds.Enqueue(location);
                    }
                });

                if (failds.Count > 0)
                {
                    obj.Locations = failds.ToArray();
                    foreach( var location in obj.Locations)
                    {
                        TransactionReporterRoute.Logger?.LogInformation($"{location.ServiceAddress}:{location.Port}重新执行事务{tranId}失败");
                    }
                    System.IO.File.WriteAllText(filepath, obj.ToJsonString(), Encoding.UTF8);
                }
                else
                {
                    File.Delete(filepath);
                }
            }
            catch (Exception)
            {

            }
        }

        public void ReportTransactionSuccess(RemoteClient remoteClient, string tranid)
        {
            if (Directory.Exists(SaveFolder) == false)
                Directory.CreateDirectory(SaveFolder);

            var filepath = Path.Combine(SaveFolder, $"{tranid}.json");
            var obj = new FileReportContent();
            obj.Locations = remoteClient._Connects.Select(m => m.InvokingInfo.ServiceLocation).ToArray();
            obj.ProxyAddress = remoteClient.ProxyAddress;
            if (remoteClient.ServiceClientCertificate != null)
            {
                obj.Cert = remoteClient.ServiceClientCertificate.RawData;
            }

            System.IO.File.WriteAllText(filepath, obj.ToJsonString(), Encoding.UTF8);
        }

        public void ReportTransactionCompleted(RemoteClient remoteClient, string tranid)
        {
            var filepath = Path.Combine(SaveFolder, $"{tranid}.json");
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }
        }

        class FileReportContent
        {
            public NetAddress ProxyAddress;
            public RegisterServiceLocation[] Locations;
            public byte[] Cert;
            public string LastError;
        }
    }
}
