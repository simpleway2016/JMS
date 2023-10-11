using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Way.Lib;
using JMS.GenerateCode;
using System.Threading.Tasks;
using JMS.Controllers;

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


        public async Task Handle(NetClient netclient, InvokeCommand cmd)
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
