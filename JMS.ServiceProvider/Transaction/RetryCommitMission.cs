using JMS.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Way.Lib;
using static JMS.RetryCommit.FaildCommitBuilder;
using JMS.Infrastructures;
using System.Net;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Claims;
using JMS.Controllers;

namespace JMS.RetryCommit
{
    class RetryCommitMission
    {
        ControllerFactory _controllerFactory;
        MicroServiceHost _microServiceHost;
        ILogger<TransactionDelegate> _loggerTran;
        IGatewayConnector _gatewayConnector;

        public RetryCommitMission(MicroServiceHost microServiceHost, ControllerFactory controllerFactory, ILogger<TransactionDelegate> loggerTran)
        {
            this._controllerFactory = controllerFactory;
            this._microServiceHost = microServiceHost;
            this._loggerTran = loggerTran;

            _gatewayConnector = _microServiceHost.ServiceProvider.GetService<IGatewayConnector>();
        }

        internal void OnGatewayReady()
        {
            retryOnBoot();
            new Thread(run).Start();
        }

        void retryOnBoot()
        {
            try
            {

                var folder = _microServiceHost.RetryCommitPath;
                if (Directory.Exists(folder) == false)
                    return;
                var files = Directory.GetFiles(folder, "*.txt");
                handleFiles(files);

                files = Directory.GetFiles(folder, "*.trying");
                handleFiles(files);

                files = Directory.GetFiles(folder, "*.err");
                handleFiles(files);
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError(ex, "重新提交事务，发生未知错误");
            }
        }

        void run()
        {

            while (true)
            {
                Thread.Sleep(5000);
                lock (this)
                {
                    try
                    {
                        var folder = _microServiceHost.RetryCommitPath;
                        if (Directory.Exists(folder))
                        {
                            var files = Directory.GetFiles(folder, "*.err");
                            handleFiles(files);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        void handleFiles(string[] files)
        {

            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    RetryFile(file, null);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tranId"></param>
        /// <returns>0:执行成功  -1：没有找到对应的事务文件 -2：执行出错</returns>
        public int RetryTranaction(string tranId, string tranFlag)
        {
            lock (this)
            {
                try
                {
                    var folder = _microServiceHost.RetryCommitPath;
                    var files = Directory.GetFiles(folder, $"{tranId}_*.*");
                    if (files.Length > 0)
                    {
                        RetryFile(files[0], tranFlag, false);
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    _loggerTran?.LogError(ex, "");
                    return -2;
                }
                return -1;
            }
        }



        public void RetryFile(string file, string tranFlag, bool checkFromGateway = true)
        {
            try
            {
                if (checkFromGateway && (DateTime.Now - new FileInfo(file).LastWriteTime).TotalSeconds < 5)
                {
                    Thread.Sleep(5000);
                }
                file = FileHelper.ChangeFileExt(file, ".trying");

                var textContent = File.ReadAllText(file, Encoding.UTF8);
                RequestInfo[] requestInfos = null;
                if (textContent.StartsWith("["))
                {
                    requestInfos = textContent.FromJson<RequestInfo[]>();
                }
                else
                {
                    requestInfos = new RequestInfo[] { textContent.FromJson<RequestInfo>() };
                }

                if (checkFromGateway == false && requestInfos.First().TransactionFlag != tranFlag)
                {
                    return;
                }
                if (checkFromGateway && _gatewayConnector.CheckTransaction(requestInfos.First().TransactionId) == false)
                {
                    _loggerTran?.LogInformation("网关没有标注事务成功，事务{0}记录到失败记录", requestInfos.First().TransactionId);
                    FileHelper.ChangeFileExt(file, ".failed");
                    return;
                }

                _loggerTran?.LogInformation("尝试重新提交事务{0}", requestInfos.First().TransactionId);



                try
                {
                    retry(requestInfos);
                    _loggerTran?.LogInformation("成功提交事务{0} 请求数据：{1}", requestInfos.First().TransactionId, requestInfos.ToJsonString());

                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggerTran?.LogError("文件{0}删除失败,{1}", file, ex.Message);
                    }

                }
                catch (Exception ex)
                {
                    _loggerTran?.LogError($"RetryCommitMission处理事务id为{requestInfos.First().TransactionId}时发生未知错误,{ex.Message}");
                    file = FileHelper.ChangeFileExt(file, ".failed");

                    //30秒后，把文件重新改回.err，重新尝试执行
                    Task.Run(() => {
                        Thread.Sleep(30000);
                        try
                        {
                            FileHelper.ChangeFileExt(file, ".err");
                        }
                        catch
                        {
                        }
                    });
                }

            }
            catch (Exception ex)
            {
                _loggerTran?.LogError($"RetryCommitMission处理事务时发生未知错误,{ex.Message}");
                try
                {
                    FileHelper.ChangeFileExt(file, ".err");
                }
                catch
                {
                }
            }

        }

        void retry(RequestInfo[] requestInfos)
        {
            using (IServiceScope serviceScope = _microServiceHost.ServiceProvider.CreateScope())
            {
                List<TransactionDelegate> transactionDelegateList = new List<TransactionDelegate>();
                foreach (var requestContent in requestInfos)
                {
                    TransactionDelegate transactionDelegate = null;
                    MicroServiceControllerBase controller = null;
                    object[] parameters = null;
                    var cmd = requestContent.Cmd;
                    try
                    {
                        BaseJmsController.RequestingObject.Value = new LocalObject(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0), cmd, serviceScope.ServiceProvider, requestContent.GetUserContent(_loggerTran));
                        var controllerTypeInfo = _controllerFactory.GetControllerType(cmd.Service);

                        controller = (MicroServiceControllerBase)_controllerFactory.CreateController(serviceScope, controllerTypeInfo);
                        controller.TransactionControl = null;


                        var methodInfo = controllerTypeInfo.Methods.FirstOrDefault(m => m.Method.Name == cmd.Method);
                        if (methodInfo == null)
                            throw new Exception($"{cmd.Service}没有提供{cmd.Method}方法");



                        var parameterInfos = methodInfo.Method.GetParameters();
                        object result = null;

                        int startPIndex = 0;
                        if (parameterInfos.Length > 0)
                        {
                            parameters = new object[parameterInfos.Length];
                            if (parameterInfos[0].ParameterType == typeof(TransactionDelegate))
                            {
                                startPIndex = 1;
                                parameters[0] = transactionDelegate = new TransactionDelegate(controller);
                                transactionDelegate.RequestCommand = cmd;
                            }
                        }

                        controller.OnBeforeAction(cmd.Method, parameters);
                        if (parameterInfos.Length > 0)
                        {
                            for (int i = startPIndex, index = 0; i < parameters.Length && index < cmd.Parameters.Length; i++, index++)
                            {
                                string pvalue = cmd.Parameters[index];
                                if (pvalue == null)
                                    continue;

                                parameters[i] = Newtonsoft.Json.JsonConvert.DeserializeObject(pvalue, parameterInfos[i].ParameterType);

                            }

                        }

                        Task.Run(async () =>
                        {
                            result = methodInfo.Method.Invoke(controller, parameters);
                            if (result is Task || result is ValueTask)
                            {
                                if (methodInfo.Method.ReturnType.IsGenericType)
                                {
                                    result = await (dynamic)result;
                                }
                                else
                                {
                                    await (dynamic)result;
                                    result = null;
                                }
                            }
                        }).Wait();


                        controller.OnAfterAction(cmd.Method, parameters);
                        if (transactionDelegate != null && transactionDelegate.SupportTransaction)
                        {
                        }
                        else if (controller.TransactionControl != null && controller.TransactionControl.SupportTransaction)
                        {
                            transactionDelegate = controller.TransactionControl;
                            transactionDelegate.RequestCommand = cmd;
                        }

                        if (transactionDelegate.StorageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == transactionDelegate.StorageEngine) == false)
                        {
                            transactionDelegateList.Add(transactionDelegate);
                        }

                    }
                    finally
                    {
                        BaseJmsController.RequestingObject.Value = null;
                    }
                }

                transactionDelegateList.CommitTransaction();
            }
        }
    }
}
