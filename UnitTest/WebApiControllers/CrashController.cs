using JMS;
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
        private readonly IScopedKeyLocker _scopedKeyLocker;
        ApiTransactionDelegate _apiTransactionDelegate;

        public static bool CanCrash = true;
        public CrashController(ApiTransactionDelegate apiTransactionDelegate, UserInfoDbContext userInfoDbContext , IScopedKeyLocker scopedKeyLocker)
        {
            this._userInfoDbContext = userInfoDbContext;
            _scopedKeyLocker = scopedKeyLocker;
            this._apiTransactionDelegate = apiTransactionDelegate;
        }

        [HttpGet]
        public string SetName(string name)
        {
            _apiTransactionDelegate.CommitAction = () => {
                if (CanCrash)
                {
                    CanCrash = false;
                    throw new Exception("故意宕机");
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
        public async void AsyncSetName2()
        {
        }
        [HttpGet]
        public string GetName()
        {
            if (_scopedKeyLocker.TryLock("testkey") == false)
                throw new Exception("分布式锁失败");
            return "Jack";
        }
    }

}