using JMS.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class JmsServiceHandler
    {
        public static bool Handle(IApplicationBuilder app, HttpContext context)
        {
            using (var netClient = new NetClient(new ConnectionStream(context)))
            {
                context.Request.Headers.TryGetValue("TranId", out StringValues tranIds);
                context.Request.Headers.TryGetValue("Tran", out StringValues supportTransactionStrs);
                
                var supportTran = !(supportTransactionStrs.Count > 0 && supportTransactionStrs[0] == "0");
                ApiTransactionDelegate.CurrentTranId.Value = tranIds[0];

                var parametersStrArr = netClient.ReadServiceData().FromJson<string[]>();

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
                                netClient.WriteServiceData(new InvokeResult
                                {
                                    Success = false,
                                    Error = "Authentication failed",

                                });
                                releaseNetClient(netClient);
                                return true;
                            }
                            context.User = authRet.Principal;
                        }
                        catch (Exception ex)
                        {
                            netClient.WriteServiceData(new InvokeResult
                            {
                                Success = false,
                                Error = ex.Message,

                            });
                            releaseNetClient(netClient);
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
                                var msg = $"转换参数出错，name:{desc.Parameters[i].Name} value:{pvalue} err:{ex.Message}";
                                netClient.WriteServiceData(new InvokeResult
                                {
                                    Success = false,
                                    Error = msg,

                                });
                                releaseNetClient(netClient);
                                return true;
                            }
                        }

                        var mvcController = controller as Controller;
                        ActionExecutedContext actionContext = null;
                        ActionExecutingContext excutingContext = null;
                        if (mvcController != null)
                        {

                            var ac = new ActionContext(context, new RouteData(), desc);
                            actionContext = new ActionExecutedContext(ac, new List<IFilterMetadata>(), mvcController);
                            Dictionary<string, object> dict = new Dictionary<string, object>();
                            for(int i = 0; i < desc.Parameters.Count; i++)
                            {
                                dict[desc.Parameters[i].Name] = parameters[i];
                            }
                            excutingContext = new ActionExecutingContext(ac, new List<IFilterMetadata>(), dict, mvcController);
                        }

                        try
                        {
                            mvcController?.OnActionExecuting(excutingContext);
                            var result = desc.MethodInfo.Invoke(controller, parameters);
                            if (mvcController != null)
                            {
                                mvcController.OnActionExecuted(actionContext);
                            }
                            

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
                            netClient.WriteServiceData(outputObj);

                            if (!supportTran)
                            {
                                releaseNetClient(netClient);
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
                            tranDelegate.WaitForCommand(gatewayConnector, failbuilder, netClient, app.ApplicationServices.GetService<ILogger>());


                        }
                        catch (Exception ex)
                        {
                            if (actionContext != null)
                            {
                                actionContext.Exception = ex;
                                mvcController.OnActionExecuted(actionContext);
                            }

                            var outputObj = new InvokeResult
                            {
                                Success = false,
                                Error = ex.Message

                            };
                            netClient.WriteServiceData(outputObj);
                        }
                        releaseNetClient(netClient);
                    }

                }
                return true;
            }
        }

        static void releaseNetClient(NetClient client)
        {
            client.Socket = null;
            client.InnerStream = null;
        }
    }
}
