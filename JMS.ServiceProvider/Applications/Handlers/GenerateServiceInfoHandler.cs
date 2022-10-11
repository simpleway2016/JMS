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

namespace JMS.Applications
{
    class GenerateServiceInfoHandler : IRequestHandler
    {
        ControllerFactory _controllerFactory;
        public InvokeType MatchType => InvokeType.GenerateServiceInfo;

        public GenerateServiceInfoHandler(ControllerFactory controllerFactory)
        {
            this._controllerFactory = controllerFactory;
        }


        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            string code = null;
            bool success;
            try 
            {
                var controllerType = _controllerFactory.GetControllerType(cmd.Service);
                var ret = TypeInfoBuilder.Build(controllerType);
                code = ret.ToJsonString();
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
