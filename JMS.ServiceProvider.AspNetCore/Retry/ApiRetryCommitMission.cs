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
using JMS.Infrastructures;
using static JMS.ServiceProvider.AspNetCore.ApiFaildCommitBuilder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.Tasks;

namespace JMS.ServiceProvider.AspNetCore
{
    class ApiRetryCommitMission
    {
        ControllerFactory _controllerFactory;
        MicroServiceHost _microServiceHost;
        ILogger<TransactionDelegate> _loggerTran;
        IGatewayConnector _gatewayConnector;
        public ApiRetryCommitMission(MicroServiceHost microServiceHost, ControllerFactory controllerFactory,  IGatewayConnector gatewayConnector, ILogger<TransactionDelegate> loggerTran)
        {
            this._controllerFactory = controllerFactory;
            this._microServiceHost = microServiceHost;
            this._loggerTran = loggerTran;

            this._gatewayConnector = gatewayConnector;
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
                    var httpContext = _microServiceHost.ServiceProvider.GetService<IHttpContextFactory>().Create(new FeatureCollection());
                    RetryFile(httpContext , file, null ,true);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tranId"></param>
        /// <returns>0:执行成功  -1：没有找到对应的事务文件 -2：执行出错</returns>
        public int RetryTranaction(HttpContext context, string tranId,string tranFlag)
        {
            lock (this)
            {
                try
                {
                    var folder = _microServiceHost.RetryCommitPath;
                    var files = Directory.GetFiles(folder, $"{tranId}_*.*");
                    if (files.Length > 0)
                    {
                        RetryFile(context, files[0], tranFlag, false);
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

        public void RetryFile(HttpContext context, string file,string tranFlag, bool checkFromGateway = true)
        {
            try
            {
                if (checkFromGateway && (DateTime.Now - new FileInfo(file).LastWriteTime).TotalSeconds < 5)
                {
                    Thread.Sleep(5000);
                }
                file = FileHelper.ChangeFileExt(file, ".trying");

                object usercontent = null;
                var filetext = File.ReadAllText(file, Encoding.UTF8);
                RequestInfo[] requestInfos;
                if (filetext.StartsWith("["))
                {
                    requestInfos = filetext.FromJson<RequestInfo[]>();
                }
                else
                {
                    requestInfos = new RequestInfo[] { filetext.FromJson<RequestInfo>() };
                }

                if (checkFromGateway && _gatewayConnector.CheckTransaction(requestInfos.First().TransactionId) == false)
                {
                    _loggerTran?.LogInformation("网关没有标注事务成功，事务{0}记录到失败记录", requestInfos.First().TransactionId);
                    FileHelper.ChangeFileExt(file, ".failed");
                    return;
                }

                if(checkFromGateway == false && tranFlag != requestInfos.First().TransactionFlag)
                {
                    return;
                }

                _loggerTran?.LogInformation("尝试重新提交事务{0}-{1}", requestInfos.First().TransactionId );

              

                retry(context, requestInfos);
                _loggerTran?.LogInformation("成功提交事务{0} 请求数据：{1}", requestInfos.First().TransactionId, requestInfos.ToJsonString());

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
                FileHelper.ChangeFileExt(file, ".failed");
            }

        }

        async void retry(HttpContext context, RequestInfo[] requestInfos)
        {

            List<ApiTransactionDelegate> transactionDelegateList = new List<ApiTransactionDelegate>();
            using (IServiceScope serviceScope = _microServiceHost.ServiceProvider.CreateScope())
            {
                context.RequestServices = serviceScope.ServiceProvider;
                foreach (var requestContent in requestInfos)
                {
                    var userContent = requestContent.GetUserContent(_loggerTran);
                    if (userContent != null)
                    {
                        context.User = userContent as ClaimsPrincipal;
                    }
                    else
                    {
                        context.User = null;
                    }
                    var controller = _controllerFactory.Create(requestContent, context.RequestServices, out ControllerActionDescriptor desc);
                    if (controller != null)
                    {
                        var parameters = new object[desc.Parameters.Count];
                        for (int i = 0; i < parameters.Length && i < requestContent.Cmd.Parameters.Length; i++)
                        {
                            string pvalue = requestContent.Cmd.Parameters[i];
                            if (pvalue == null)
                                continue;

                            try
                            {
                                parameters[i] = Newtonsoft.Json.JsonConvert.DeserializeObject(pvalue, desc.Parameters[i].ParameterType);
                            }
                            catch (Exception ex)
                            {
                                var msg = $"转换参数出错，name:{desc.Parameters[i].Name} value:{pvalue}";
                                throw new Exception(msg, ex);
                            }
                        }

                        var actionFilterProcessor = new ActionFilterProcessor(context, controller, desc, parameters);

                        actionFilterProcessor.OnActionExecuting();
                        var result = desc.MethodInfo.Invoke(controller, parameters);
                        if (result is Task || result is ValueTask)
                        {
                            if (desc.MethodInfo.ReturnType.IsGenericType)
                            {
                                result = await (dynamic)result;
                            }
                            else
                            {
                                await (dynamic)result;
                                result = null;
                            }
                        }

                        result = actionFilterProcessor.OnActionExecuted(result);

                        var transactionDelegate = context.RequestServices.GetService<ApiTransactionDelegate>();
                        if (transactionDelegate.StorageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == transactionDelegate.StorageEngine) == false)
                        {
                            transactionDelegateList.Add(transactionDelegate);
                        }
                    }
                    else
                    {
                        throw new Exception("controller is null");
                    }
                }

                transactionDelegateList.CommitTransaction();
            }
        }
    }
}
