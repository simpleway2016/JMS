using JMS;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal class TestScopeController : MicroServiceControllerBase
    {
        readonly IScopedKeyLocker _keyLocker;
        UserInfoDbContext _userInfoDbContext;
        public TestScopeController(UserInfoDbContext userInfoDbContext, IScopedKeyLocker keyLocker)
        {
            this._keyLocker = keyLocker;
            this._userInfoDbContext = userInfoDbContext;
        }

        public async Task SetUserName(string name)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.UserName = name;
        }
        public void SetFather(string father)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.Father = father;
        }
        public void SetAge(int age)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.Age = age;
        }

        public void TestLockScopedKey()
        {
           if(!_keyLocker.TryLock("MyScopedKey"))
            {
                throw new Exception("lock key failed");
            }
        }


        public async Task TestLockKey()
        {
            var key = "TestKey";
            var ret = _keyLocker.TryLock( key);
            if (ret == false)
                throw new Exception("TryLock失败");

            if (_keyLocker.GetLockedKeys().Contains(key) == false)
                throw new Exception("TryLock失败");

            ret = _keyLocker.TryUnLock(key);
            if (ret == false)
                throw new Exception("TryUnLock失败");

            if (_keyLocker.GetLockedKeys().Length != 0)
                throw new Exception("TryLock失败");

            ret = _keyLocker.TryLock(key);
            if (ret == false)
                throw new Exception("TryLock失败");

            if (_keyLocker.GetLockedKeys().Contains(key) == false)
                throw new Exception("TryLock失败");

            _keyLocker.UnLockAnyway(key);

            if (_keyLocker.GetLockedKeys().Length != 0)
                throw new Exception("TryLock失败");


            ////异步
            ret = await _keyLocker.TryLockAsync( key);
            if (ret == false)
                throw new Exception("TryLock失败");

            if (_keyLocker.GetLockedKeys().Contains(key) == false)
                throw new Exception("TryLock失败");

            ret = await _keyLocker.TryUnLockAsync(key);
            if (ret == false)
                throw new Exception("TryUnLock失败");

            if (_keyLocker.GetLockedKeys().Length != 0)
                throw new Exception("TryLock失败");

            ret = await _keyLocker.TryLockAsync(key);
            if (ret == false)
                throw new Exception("TryLock失败");

            if (_keyLocker.GetLockedKeys().Contains(key) == false)
                throw new Exception("TryLock失败");

            await _keyLocker.UnLockAnywayAsync(key);

            if (_keyLocker.GetLockedKeys().Length != 0)
                throw new Exception("TryLock失败");
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);

            if (_userInfoDbContext.BeganTransaction)
            {
                this.TransactionControl = new JMS.TransactionDelegate(this, _userInfoDbContext);
            }
        }
    }
}
