using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal class TestUserInfoController : MicroServiceControllerBase
    {
        UserInfoDbContext _userInfoDbContext;
        public TestUserInfoController(UserInfoDbContext userInfoDbContext)
        {
            this._userInfoDbContext = userInfoDbContext;
            if (this.ServiceProvider.GetService<UserInfoDbContext>() != userInfoDbContext)
                throw new Exception("作用域对象出错");
        }

        public void SetUserName(string name)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.UserName = name;
        }

        public void SetAge(int age)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.Age = age;
        }

        public void SetFather(string father)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.Father = father;
        }

        public void SetMather(string name)
        {
            //启动支持分布式事务
            _userInfoDbContext.BeginTransaction();

            _userInfoDbContext.Mather = name;
        }

        public void LockName(string name)
        {
            this.TryLock(name);
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);

            if (_userInfoDbContext.BeganTransaction)
            {
                this.TransactionControl = new JMS.TransactionDelegate(this.TransactionId);
                this.TransactionControl.CommitAction = () => {
                    _userInfoDbContext.CommitTransaction();
                };

                this.TransactionControl.RollbackAction = () => {
                    _userInfoDbContext.RollbackTransaction();
                };
            }
        }
    }
}
