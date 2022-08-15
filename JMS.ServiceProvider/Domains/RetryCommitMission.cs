using JMS.Dtos;
using JMS.Domains;
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

namespace JMS.RetryCommit
{
    internal class RetryCommitMission
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

        internal void OnGatewayConnected()
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

        void handleFiles(string[] files)
        {
           
            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    RetryFile(file);
                }
            }
        }

        public void RetryFile(string file)
        {
            try
            {
                object usercontent = null;
                var fileContent = File.ReadAllText(file, Encoding.UTF8).FromJson<RequestInfo>();

                if(_gatewayConnector.CheckTransaction(fileContent.TransactionId) == false)
                {
                    _loggerTran?.LogInformation("网关没有标注事务成功，事务{0}记录到失败记录", fileContent.TransactionId);
                    File.Move(file, file + ".faild");
                    return;
                }

                _loggerTran?.LogInformation("尝试重新提交事务{0}-{1}", fileContent.TransactionId, fileContent.Cmd.Method);

                if (fileContent.UserContentValue != null)
                {

                    try
                    {
                        if (fileContent.UserContentType == typeof(System.Security.Claims.ClaimsPrincipal))
                        {

                            byte[] bs = fileContent.UserContentValue.FromJson<byte[]>();
                            using (var ms = new System.IO.MemoryStream(bs))
                            {
                                usercontent = new System.Security.Claims.ClaimsPrincipal(new BinaryReader(ms));
                            }
                        }
                        else
                        {
                            usercontent = Newtonsoft.Json.JsonConvert.DeserializeObject(fileContent.UserContentValue, fileContent.UserContentType);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggerTran?.LogError("RetryCommitMission无法还原事务id为{0}的身份信息,{1}", fileContent.TransactionId, ex.Message);
                        File.Move(file, file + ".faild");
                    }


                }

                retry(fileContent.Cmd, usercontent);
                _loggerTran?.LogInformation("成功提交事务{0} 请求数据：{1}", fileContent.TransactionId, fileContent.Cmd.ToJsonString());

                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _loggerTran?.LogError("文件{0}删除失败,{1}", file, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("RetryCommitMission处理事务id为{0}时发生未知错误,{1}", ex.Message);
                File.Move(file, file + ".faild");
            }
        }

        void retry(InvokeCommand cmd, object userContent)
        {
            TransactionDelegate transactionDelegate = null;
            MicroServiceControllerBase controller = null;
            object[] parameters = null;

            try
            {
                MicroServiceControllerBase.RequestingCommand.Value = cmd;
                var controllerTypeInfo = _controllerFactory.GetControllerType(cmd.Service);

                controller = _controllerFactory.CreateController(controllerTypeInfo);
                controller.UserContent = userContent;
                controller._keyLocker = _microServiceHost.ServiceProvider.GetService<IKeyLocker>();


                var methodInfo = controllerTypeInfo.Methods.FirstOrDefault(m => m.Method.Name == cmd.Method);
                if (methodInfo == null)
                    throw new Exception($"{cmd.Service}没有提供{cmd.Method}方法");


                MicroServiceControllerBase.Current = controller;

                var parameterInfos = methodInfo.Method.GetParameters();
                object result = null;

                int startPIndex = 0;
                if (parameterInfos.Length > 0)
                {
                    parameters = new object[parameterInfos.Length];
                    if (parameterInfos[0].ParameterType == typeof(TransactionDelegate))
                    {
                        startPIndex = 1;
                        parameters[0] = transactionDelegate = new TransactionDelegate(cmd.Header["TranId"]);
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
                result = methodInfo.Method.Invoke(controller, parameters);
                controller.OnAfterAction(cmd.Method, parameters);
                if (transactionDelegate != null && (transactionDelegate.CommitAction != null || transactionDelegate.RollbackAction != null))
                {
                }
                else if (controller.TransactionControl != null && (controller.TransactionControl.CommitAction != null || controller.TransactionControl.RollbackAction != null))
                {
                    transactionDelegate = controller.TransactionControl;
                    transactionDelegate.RequestCommand = cmd;
                }

                if (transactionDelegate != null && transactionDelegate.CommitAction != null)
                {
                    transactionDelegate.CommitAction();
                }
                transactionDelegate = null;

            }
            finally
            {
                MicroServiceControllerBase.Current = null;
                controller?.OnUnLoad();
                MicroServiceControllerBase.RequestingCommand.Value = null;
            }
        }
    }
}
