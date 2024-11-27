using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using System.Linq;
using System.Reflection;
using JMS.RetryCommit;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Security.Authentication;
using JMS.Controllers;
using System.ComponentModel.DataAnnotations;
using JMS.ServerCore.Http;
using JMS.Common.Json;

namespace JMS.Applications
{
    class InvokeRequestHandler : IRequestHandler
    {
        ControllerFactory _controllerFactory;
        IGatewayConnector _gatewayConnector;
        FaildCommitBuilder _faildCommitBuilder;
        MicroServiceHost _MicroServiceProvider;
        ILogger<InvokeRequestHandler> _logger;
        ILogger<TransactionDelegate> _loggerTran;

        static string LastInvokingMsgString;
        static DateTime LastInvokingMsgStringTime = DateTime.Now.AddDays(-1);

        public InvokeRequestHandler(ILogger<InvokeRequestHandler> logger,
             ILogger<TransactionDelegate> loggerTran,
            MicroServiceHost microServiceProvider,
            IGatewayConnector gatewayConnector,
            ControllerFactory controllerFactory,
            FaildCommitBuilder faildCommitBuilder)
        {
            this._controllerFactory = controllerFactory;
            this._gatewayConnector = gatewayConnector;
            this._faildCommitBuilder = faildCommitBuilder;
            _MicroServiceProvider = microServiceProvider;
            _logger = logger;
            _loggerTran = loggerTran;
        }

        public InvokeType MatchType => InvokeType.Invoke;
        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {
            using (IServiceScope serviceScope = _MicroServiceProvider.ServiceProvider.CreateScope())
            {
                await handleInScope(netclient, cmd, serviceScope);
            }
        }

        static bool checkRoles(List<JMS.AuthorizeAttribute> authorizeAttributes, ClaimsPrincipal claimsPrincipal)
        {
            if (authorizeAttributes.Count > 0)
            {
                foreach (var authAttItem in authorizeAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(authAttItem.Roles))
                    {
                        var attRoles = authAttItem.Roles.Split(',');
                        if (attRoles.Any(x => claimsPrincipal.IsInRole(x.Trim())) == false)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        async Task handleInScope(NetClient netclient, InvokeCommand cmd, IServiceScope serviceScope)
        {
            List<TransactionDelegate> tranDelegateList = null;
            var originalTimeout = netclient.ReadTimeout;
            bool logTrace = _logger != null && _logger.IsEnabled(LogLevel.Trace);
            while (true)
            {
                if (logTrace)
                {
                    var str = string.Format("invoke service:{0} headers{3} method:{1} parameters:{2}", cmd.Service, cmd.Method, cmd.Parameters.ToJsonString(true), cmd.Header.ToJsonString(true));
                    if (str != LastInvokingMsgString || (DateTime.Now - LastInvokingMsgStringTime).TotalSeconds > 5)
                    {
                        LastInvokingMsgStringTime = DateTime.Now;
                        LastInvokingMsgString = str;
                        _logger?.LogTrace(str);
                    }
                }

                TransactionDelegate transactionDelegate = null;
                MicroServiceControllerBase controller = null;
                object[] parameters = null;

                try
                {
                    var controllerTypeInfo = _controllerFactory.GetControllerType(cmd.Service);
                    var methodInfo = controllerTypeInfo.Methods.FirstOrDefault(m => m.Method.Name == cmd.Method);
                    if (methodInfo == null)
                        throw new Exception($"{cmd.Service}没有提供{cmd.Method}方法");

                    ClaimsPrincipal userContent = null;
                    if (methodInfo.AllowAnonymous == false)
                    {
                        if (controllerTypeInfo.NeedAuthorize || methodInfo.NeedAuthorize)
                        {
                            var auth = _MicroServiceProvider.ServiceProvider.GetService<IAuthenticationHandler>();
                            if (auth != null)
                            {
                                if (logTrace)
                                {
                                    _logger.LogTrace("Authenticating...");
                                }

                                userContent = auth.Authenticate(cmd.Header);
                                if (userContent is ClaimsPrincipal principal)
                                {
                                    if (!checkRoles(methodInfo.AuthorizeAttributes, principal))
                                    {
                                        throw new AuthenticationException("Authentication failed for roles");
                                    }
                                }
                            }
                        }
                    }


                    BaseJmsController.RequestingObject.Value = new LocalObject(netclient.RemoteEndPoint, cmd, serviceScope.ServiceProvider, userContent);

                    controller = (MicroServiceControllerBase)_controllerFactory.CreateController(serviceScope, controllerTypeInfo);
                    controller.TransactionControl = null;
                    controller.NetClient = netclient;

                    var parameterInfos = methodInfo.Parameters;
                    object result = null;

                    int startPIndex = 0;
                    if (parameterInfos.Length > 0)
                    {
                        parameters = new object[parameterInfos.Length];
                        if (parameterInfos[0].IsTransactionDelegate)
                        {
                            startPIndex = 1;
                            parameters[0] = transactionDelegate = new TransactionDelegate(controller);
                            transactionDelegate.RequestCommand = cmd;
                        }
                    }

                    if (parameterInfos.Length > 0)
                    {
                        if (cmd.Parameters == null)
                            throw new Exception("微服务客户端没有正确传递参数");

                        for (int i = startPIndex, index = 0; i < parameters.Length && index < cmd.Parameters.Length; i++, index++)
                        {
                            string pvalue = cmd.Parameters[index];
                            if (pvalue == null)
                                continue;

                            var pinfo = parameterInfos[i];
                            try
                            {
                                parameters[i] = ApplicationJsonSerializer.JsonSerializer.Deserialize(pvalue, pinfo.ParameterInfo.ParameterType);

                            }
                            catch (Exception ex)
                            {
                                var msg = $"转换参数出错，name:{parameterInfos[i].ParameterInfo.Name} value:{pvalue} err:{ex.Message}";
                                netclient.WriteServiceData(new InvokeResult
                                {
                                    Success = false,
                                    Error = msg
                                });
                                return;
                            }

                            if (_MicroServiceProvider.SuppressModelStateInvalidFilter == false && parameters[i] != null && pinfo.IsRequiredValidation)
                            {
                                try
                                {
                                    var validationResult = controller.ValidateModel(methodInfo.Method.Name, pinfo.ParameterInfo.Name, parameters[i]);
                                    if (validationResult.Success == false)
                                    {
                                        netclient.WriteServiceData(new InvokeResult
                                        {
                                            Success = false,
                                            Error = validationResult.Error
                                        });
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    netclient.WriteServiceData(new InvokeResult
                                    {
                                        Success = false,
                                        Error = ex.Message
                                    });
                                    return;
                                }
                            }
                        }

                    }

                    controller.OnBeforeAction(cmd.Method, parameters);

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

                    controller.OnAfterAction(cmd.Method, parameters);

                    var supportTran = false;
                    if (transactionDelegate != null && transactionDelegate.SupportTransaction)
                    {
                        supportTran = true;
                    }
                    else if (controller.TransactionControl != null && controller.TransactionControl.SupportTransaction)
                    {
                        transactionDelegate = controller.TransactionControl;
                        transactionDelegate.RequestCommand = cmd;
                        supportTran = true;
                    }

                    if (supportTran && cmd.Header.ContainsKey("Tran") && cmd.Header["Tran"] == "0")
                    {
                        //调用端不需要事务支持
                        supportTran = false;
                    }

                    if (!supportTran && transactionDelegate != null)
                    {
                        //不需要事务支持，提交现有事务
                        transactionDelegate.CommitTransaction();
                        transactionDelegate = null;
                    }

                    if (result is HttpResult httpResult)
                    {
                        if (httpResult.StatusCode >= 400)
                        {
                            //执行失败，回滚事务
                            if (transactionDelegate != null)
                            {
                                try
                                {
                                    if (transactionDelegate.StorageEngine == null || tranDelegateList == null || tranDelegateList.Any(x => x.StorageEngine == transactionDelegate.StorageEngine) == false)
                                    {
                                        transactionDelegate.RollbackTransaction();
                                    }
                                }
                                catch (Exception rollex)
                                {
                                    _logger?.LogError(rollex, rollex.Message);
                                }
                                transactionDelegate = null;
                            }
                            tranDelegateList.RollbackTransaction();

                            netclient.WriteServiceData(new InvokeResult
                            {
                                Success = false,
                                Data = httpResult.Data,
                                Error = httpResult.Data is string ? (string)httpResult.Data : null,
                                Attributes = $"{{'StatusCode':{httpResult.StatusCode}}}",

                            });
                        }
                        else
                        {
                            netclient.WriteServiceData(new InvokeResult
                            {
                                Success = true,
                                SupportTransaction = supportTran,
                                Data = httpResult.Data,
                                Attributes = $"{{'StatusCode':{httpResult.StatusCode}}}",

                            });
                        }
                    }
                    else
                    {
                        netclient.WriteServiceData(new InvokeResult
                        {
                            Success = true,
                            SupportTransaction = supportTran,
                            Data = result

                        });
                    }

                    if (!supportTran)
                    {
                        return;
                    }

                    if (transactionDelegate != null)
                    {
                        netclient.ReadTimeout = 0;
                        transactionDelegate.UserContent = controller.UserContent;
                        var nextInvokeCmd = await transactionDelegate.WaitForCommandAsync(tranDelegateList, _gatewayConnector, _faildCommitBuilder, netclient, _loggerTran);

                        if (nextInvokeCmd != null)
                        {
                            bool addToList = true;
                            if (tranDelegateList == null)
                            {
                                tranDelegateList = new List<TransactionDelegate>();
                            }
                            else
                            {
                                if (transactionDelegate.StorageEngine != null)
                                {
                                    if (tranDelegateList.Any(x => x.StorageEngine == transactionDelegate.StorageEngine))
                                    {
                                        //同一个数据库对象，不用放入list
                                        addToList = false;
                                    }
                                }
                            }

                            if (addToList)
                            {
                                tranDelegateList.Add(transactionDelegate);
                            }
                            //继续调用下一个方法
                            cmd = nextInvokeCmd;
                            continue;
                        }

                        transactionDelegate = null;
                    }
                }
                catch (ResponseEndException)
                {
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null)
                        ex = ex.InnerException;

                    if (transactionDelegate != null)
                    {
                        try
                        {
                            if (transactionDelegate.StorageEngine == null || tranDelegateList == null || tranDelegateList.Any(x => x.StorageEngine == transactionDelegate.StorageEngine) == false)
                            {
                                transactionDelegate.RollbackTransaction();
                            }
                        }
                        catch (Exception rollex)
                        {
                            _logger?.LogError(rollex, rollex.Message);
                        }
                        transactionDelegate = null;
                    }
                    tranDelegateList.RollbackTransaction();

                    try
                    {
                        if (controller?.OnInvokeError(cmd.Method, parameters, ex) == false)
                        {
                            _logger?.LogError(ex, ex.Message);
                        }
                    }
                    catch (ResponseEndException)
                    {
                        return;
                    }

                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }

                    if (ex is AuthenticationException)
                    {
                        netclient.WriteServiceData(new InvokeResult
                        {
                            Success = false,
                            Data = 401,
                            Error = ex.Message
                        });
                    }
                    else
                    {
                        netclient.WriteServiceData(new InvokeResult
                        {
                            Success = false,
                            Error = ex.Message
                        });
                    }
                    return;
                }
                finally
                {
                    netclient.ReadTimeout = originalTimeout;
                    BaseJmsController.RequestingObject.Value = null;
                }

                return;
            }

        }
    }
}
