using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Way.Lib;
using JMS.Infrastructures;
using JMS.GenerateCode;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JMS.Applications
{
    class GenerateInvokeCodeRequestHandler : IRequestHandler
    {
        public InvokeType MatchType => InvokeType.GenerateInvokeCode;
        ICodeBuilder _codeBuilder;
        private readonly ILogger<GenerateInvokeCodeRequestHandler> _logger;

        public GenerateInvokeCodeRequestHandler(ICodeBuilder codeBuilder,ILogger<GenerateInvokeCodeRequestHandler> logger)
        {
            _codeBuilder = codeBuilder;
            _logger = logger;
        }

        bool? _IsDebug;
        bool IsDebug
        {
            get
            {
                if (_IsDebug == null)
                {
                    try
                    {
                        var assembly = Assembly.GetEntryAssembly();
                        if (assembly == null)
                        {
                            // 由于调用 GetFrames 的 StackTrace 实例没有跳过任何帧，所以 GetFrames() 一定不为 null。
                            assembly = new StackTrace().GetFrames().Last().GetMethod().Module.Assembly;
                        }

                        var debuggableAttribute = assembly.GetCustomAttribute<DebuggableAttribute>();
                        _IsDebug = debuggableAttribute.DebuggingFlags
                            .HasFlag(DebuggableAttribute.DebuggingModes.EnableEditAndContinue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "");
                        _IsDebug = false;
                    }
                  
                }

                return _IsDebug.Value;
            }
        }

        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {
            //if( !IsDebug )
            //{
            //    netclient.WriteServiceData(new InvokeResult()
            //    {
            //        Data = "not support in release",
            //        Success = false
            //    });
            //    return;
            //}

            string code = null;
            bool success;
            try 
            {
                code = _codeBuilder.GenerateCode(cmd.Parameters[0], cmd.Parameters[1], cmd.Service);
                success = true;
            }
            catch(Exception ex)
            {
                code = ex.ToString();
                success = false;
            }
            netclient.WriteServiceData(new InvokeResult()
            {
                Data = code,
                Success = success
            });
        }
    }
}
