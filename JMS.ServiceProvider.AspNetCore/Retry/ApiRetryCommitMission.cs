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
                file = FileHelper.ChangeFileExt(file, ".trying");

                object usercontent = null;
                var fileContent = File.ReadAllText(file, Encoding.UTF8).FromJson<RequestInfo>();

                if (checkFromGateway && _gatewayConnector.CheckTransaction(fileContent.TransactionId) == false)
                {
                    _loggerTran?.LogInformation("网关没有标注事务成功，事务{0}记录到失败记录", fileContent.TransactionId);
                    FileHelper.ChangeFileExt(file, ".failed");
                    return;
                }

                if(checkFromGateway == false && tranFlag != fileContent.TransactionFlag)
                {
                    return;
                }

                _loggerTran?.LogInformation("尝试重新提交事务{0}-{1}", fileContent.TransactionId, fileContent.Cmd.ControllerFullName + "." + fileContent.Cmd.ActionName);

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
                        FileHelper.ChangeFileExt(file, ".failed");
                    }


                }

                retry(context, fileContent, usercontent);
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
                FileHelper.ChangeFileExt(file, ".failed");
            }

        }

        void retry(HttpContext context, RequestInfo requestInfo, object userContent)
        {
            if(userContent != null)
            {
                context.User = userContent as ClaimsPrincipal;
            }
            using (IServiceScope serviceScope = _microServiceHost.ServiceProvider.CreateScope())
            {
                context.RequestServices = serviceScope.ServiceProvider;

                var controller = _controllerFactory.Create(requestInfo, context.RequestServices, out ControllerActionDescriptor desc);
                if (controller != null)
                {
                    var parameters = new object[desc.Parameters.Count];
                    for (int i = 0; i < parameters.Length && i < requestInfo.Cmd.Parameters.Length; i++)
                    {
                        string pvalue = requestInfo.Cmd.Parameters[i];
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
                    if (result is Task t)
                    {
                        t.Wait();

                        if (desc.MethodInfo.ReturnType.IsGenericType)
                        {
                            result = desc.MethodInfo.ReturnType.GetProperty(nameof(Task<int>.Result)).GetValue(t);
                        }
                        else
                        {
                            result = null;
                        }
                    }

                    result = actionFilterProcessor.OnActionExecuted(result);

                    var tranDelegate = context.RequestServices.GetService<ApiTransactionDelegate>();
                    if (tranDelegate.CommitAction != null)
                    {
                        tranDelegate.CommitAction();
                        tranDelegate.CommitAction = null;
                    }
                }
                else
                {
                    throw new Exception("controller is null");
                }
            }
        }
    }
}
