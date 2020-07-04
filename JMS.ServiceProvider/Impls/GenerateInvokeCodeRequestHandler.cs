using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class GenerateInvokeCodeRequestHandler : IRequestHandler
    {
        public InvokeType MatchType => InvokeType.GenerateInvokeCode;
        ICodeBuilder _codeBuilder;
        public GenerateInvokeCodeRequestHandler(ICodeBuilder codeBuilder)
        {
            _codeBuilder = codeBuilder;
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
                        _IsDebug = false;
                    }
                  
                }

                return _IsDebug.Value;
            }
        }

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            if( !IsDebug )
            {
                netclient.WriteServiceData(new InvokeResult()
                {
                    Data = "not support in release",
                    Success = true
                });
                return;
            }

            var code = _codeBuilder.GenerateCode(cmd.Parameters[0],cmd.Parameters[1], cmd.Service);
            netclient.WriteServiceData(new InvokeResult()
            {
                Data = code,
                Success = true
            });
        }
    }
}
