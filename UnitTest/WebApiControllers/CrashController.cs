using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UnitTest.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class CrashController : ControllerBase
    {
        ApiTransactionDelegate _apiTransactionDelegate;
        public static string FinallyUserName;
        public static bool CanCrash = true;
        public CrashController(ApiTransactionDelegate apiTransactionDelegate)
        {
            this._apiTransactionDelegate = apiTransactionDelegate;
        }

        string _name;
        [HttpGet]
        public string SetName(string name)
        {
            _apiTransactionDelegate.CommitAction = () => {
                if (CanCrash)
                {
                    CanCrash = false;
                    throw new Exception("¹ÊÒâå´»ú");
                }

                FinallyUserName = _name;

            };
            _name = name;
            return name;
        }

        [HttpGet]
        public async Task AsyncSetName(string name)
        {
            _apiTransactionDelegate.CommitAction = () => {
                FinallyUserName = _name;
            };
            _name = name;
        }

        [HttpGet]
        public string GetName()
        {
            return "Jack";
        }
    }

}