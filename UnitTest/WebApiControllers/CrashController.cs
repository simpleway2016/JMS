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
using UnitTest.ServiceHosts;

namespace UnitTest.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class CrashController : ControllerBase
    {
        UserInfoDbContext _userInfoDbContext;
        ApiTransactionDelegate _apiTransactionDelegate;

        public static bool CanCrash = true;
        public CrashController(ApiTransactionDelegate apiTransactionDelegate, UserInfoDbContext userInfoDbContext)
        {
            this._userInfoDbContext = userInfoDbContext;
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

                UserInfoDbContext.FinallyUserName = name;

            };
            return name;
        }

        [HttpGet]
        public async Task AsyncSetName(string name)
        {
            _userInfoDbContext.BeginTransaction();
            _apiTransactionDelegate.StorageEngine = _userInfoDbContext;
            _userInfoDbContext.UserName = name;
        }

        [HttpGet]
        public string GetName()
        {
            return "Jack";
        }
    }

}