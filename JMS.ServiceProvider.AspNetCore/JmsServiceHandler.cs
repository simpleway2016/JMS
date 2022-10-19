using JMS.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class JmsServiceHandler
    {
        public static bool Handle(IApplicationBuilder app, HttpContext context)
        {
            using (var wsClient = new WSClient(context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false).GetAwaiter().GetResult()))
            {

                context.Request.Headers.TryGetValue("TranId", out StringValues tranIds);
                context.Request.Headers.TryGetValue("Tran", out StringValues supportTransactionStrs);
                ApiTransactionDelegate.CurrentTranId.Value = tranIds[0];
                var supportTran = !(supportTransactionStrs.Count > 0 && supportTransactionStrs[0] == "0");


                var parametersStrArr = wsClient.ReceiveData().FromJson<string[]>();

                var controllerFactory = app.ApplicationServices.GetService<ControllerFactory>();

                using (var scope = app.ApplicationServices.CreateScope())
                {
                    var controller = controllerFactory.Create(context, scope.ServiceProvider, out ControllerActionDescriptor desc);

                    var author = desc.MethodInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
                    if (author == null)
                        author = desc.ControllerTypeInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();

                    if (author != null)
                    {
                        try
                        {
                            var authRet = context.AuthenticateAsync(author.AuthenticationSchemes).ConfigureAwait(false).GetAwaiter().GetResult();

                            if (authRet.Succeeded == false)
                            {
                                wsClient.Close(WebSocketCloseStatus.Empty, "Authentication failed");
                                return true;
                            }
                            context.User = authRet.Principal;
                        }
                        catch (Exception ex)
                        {
                            wsClient.Close(WebSocketCloseStatus.InternalServerError, ex.Message);
                            return true;
                        }
                    }


                    if (controller != null)
                    {
                        var parameters = new object[desc.Parameters.Count];
                        for (int i = 0; i < parameters.Length && i < parametersStrArr.Length; i++)
                        {
                            string pvalue = parametersStrArr[i];
                            if (pvalue == null)
                                continue;

                            try
                            {
                                parameters[i] = Newtonsoft.Json.JsonConvert.DeserializeObject(pvalue, desc.Parameters[i].ParameterType);
                            }
                            catch (Exception ex)
                            {
                                var msg = $"转换参数出错，name:{desc.Parameters[i].Name} value:{pvalue}";
                                wsClient.Close(WebSocketCloseStatus.InternalServerError, msg);
                                return true;
                            }
                        }
                        try
                        {
                            var result = desc.MethodInfo.Invoke(controller, parameters);

                            var tranDelegate = scope.ServiceProvider.GetService<ApiTransactionDelegate>();
                            if (tranDelegate.CommitAction != null && !supportTran)
                            {
                                tranDelegate.CommitAction();
                                tranDelegate = null;
                            }
                            else if (tranDelegate.CommitAction == null)
                            {
                                tranDelegate = null;
                                supportTran = false;
                            }

                            var outputObj = new InvokeResult
                            {
                                Success = true,
                                SupportTransaction = supportTran,
                                Data = result

                            };
                            wsClient.SendData(outputObj.ToJsonString());

                            if (!supportTran)
                            {
                                wsClient.Close(WebSocketCloseStatus.NormalClosure, null);
                                return true;
                            }

                            tranDelegate.InvokeInfo = new InvokeInfo()
                            {
                                ActionName = desc.ActionName,
                                ControllerFullName = desc.ControllerTypeInfo.FullName,
                                Parameters = parametersStrArr
                            };

                            tranDelegate.UserContent = context.User;

                            var failbuilder = app.ApplicationServices.GetService<ApiFaildCommitBuilder>();
                            var gatewayConnector = app.ApplicationServices.GetService<IGatewayConnector>();
                            tranDelegate.WaitForCommand(gatewayConnector, failbuilder, wsClient, app.ApplicationServices.GetService<ILogger>());


                        }
                        catch (Exception ex)
                        {
                            var outputObj = new InvokeResult
                            {
                                Success = false,
                                Error = ex.Message

                            };
                            wsClient.SendData(outputObj.ToJsonString());
                        }
                        wsClient.Close(WebSocketCloseStatus.NormalClosure, null);
                    }

                }
                return true;
            }
        }
    }
}
