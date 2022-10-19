﻿using JMS.Dtos;
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

      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tranId"></param>
        /// <returns>0:执行成功  -1：没有找到对应的事务文件 -2：执行出错</returns>
        public int RetryTranaction(HttpContext context, string tranId)
        {
            lock (this)
            {
                try
                {
                    var folder = _microServiceHost.RetryCommitPath;
                    var files = Directory.GetFiles(folder, $"{tranId}_*.*");
                    if (files.Length > 0)
                    {
                        RetryFile(context, files[0], false);
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

        public void RetryFile(HttpContext context, string file, bool checkFromGateway = true)
        {
            try
            {
                file = FileHelper.ChangeFileExt(file, ".trying");

                object usercontent = null;
                var fileContent = File.ReadAllText(file, Encoding.UTF8).FromJson<RequestInfo>();

                if (checkFromGateway && _gatewayConnector.CheckTransaction(fileContent.TransactionId) == false)
                {
                    _loggerTran?.LogInformation("网关没有标注事务成功，事务{0}记录到失败记录", fileContent.TransactionId);
                    FileHelper.ChangeFileExt(file, ".faild");
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
                        FileHelper.ChangeFileExt(file, ".faild");
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
                FileHelper.ChangeFileExt(file, ".faild");
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
                var controller = _controllerFactory.Create(requestInfo, serviceScope.ServiceProvider, out ControllerActionDescriptor desc);
                if (controller != null)
                {
                    if(userContent != null)
                    {

                    }

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
                    var result = desc.MethodInfo.Invoke(controller, parameters);

                    var tranDelegate = serviceScope.ServiceProvider.GetService<ApiTransactionDelegate>();
                    if (tranDelegate.CommitAction != null)
                    {
                        tranDelegate.CommitAction();
                    }
                }
            }
        }
    }
}
