using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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


        [HttpGet]
        public string SetName(string name)
        {
            _apiTransactionDelegate.CommitAction = () => {
                if (CanCrash)
                {
                    CanCrash = false;
                    throw new Exception("π “‚Â¥ª˙");
                }

                FinallyUserName = name;

            };
            return name;
        }

        [HttpGet]
        public async void AsyncSetName()
        {
            
        }

        [HttpGet]
        public string GetName()
        {
            return "Jack";
        }
    }

}