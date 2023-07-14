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
        class CrashControllerDB : IStorageEngine
        {
           
            string _name;
            public CrashControllerDB(string name)
            {
                this._name = name;

            }
            public object CurrentTransaction => _name;

            public void BeginTransaction()
            {
            }

            public void CommitTransaction()
            {
                if (CrashController.CanCrash)
                {
                    CrashController.CanCrash = false;
                    throw new Exception("故意宕机");
                }

                UserInfoDbContext.FinallyUserName = _name;
            }

            public void RollbackTransaction()
            {
            }
        }

        public static bool CanCrash = true;
        UserInfoDbContext _userInfoDbContext;
        private readonly IScopedKeyLocker _scopedKeyLocker;
        ApiTransactionDelegate _apiTransactionDelegate;

       
        public CrashController(ApiTransactionDelegate apiTransactionDelegate, UserInfoDbContext userInfoDbContext , IScopedKeyLocker scopedKeyLocker)
        {
            this._userInfoDbContext = userInfoDbContext;
            _scopedKeyLocker = scopedKeyLocker;
            this._apiTransactionDelegate = apiTransactionDelegate;
        }

        [HttpGet]
        public string SetName(string name)
        {
            _apiTransactionDelegate.StorageEngine = new CrashControllerDB(name);
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