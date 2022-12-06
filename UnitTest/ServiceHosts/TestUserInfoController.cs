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
    internal class AsyncDemoController : MicroServiceControllerBase
    {
        public async void ErrorDemo()
        {
            await Task.Delay(10);
        }
    }
    internal class TestUserInfoController : MicroServiceControllerBase
    {
        UserInfoDbContext _userInfoDbContext;
        public TestUserInfoController(UserInfoDbContext userInfoDbContext)
        {
            this._userInfoDbContext = userInfoDbContext;
            if (this.ServiceProvider.GetService<UserInfoDbContext>() != userInfoDbContext)
                throw new Exception("作用域对象出错");
        }
        public void CheckTranId()
        {
            if (string.IsNullOrEmpty(this.TransactionId))
                throw new Exception("TransactionId为空");
        }
       
        public string GetMyName()
        {
            return "Jack";
        }
        public string GetMyNameError()
        {
            throw new Exception("ErrMsg");
            return "Jack";
        }
        public async Task SetUserName(string name)
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

        public void BeError()
        {
            throw new Exception("有意触发错误");
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

        public void LockName(string name,string a2,string a3,string a4)
        {
            this.TryLock(name);
            this.TryLock(a2);
            this.TryLock(a3);
            this.TryLock(a4);
        }

        public void UnlockName(string name)
        {
            this.TryUnLock(name);
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);

            if (_userInfoDbContext.BeganTransaction)
            {
                this.TransactionControl = new JMS.TransactionDelegate(this);
                this.TransactionControl.CommitAction = () => {
                    _userInfoDbContext.CommitTransaction();
                };

                this.TransactionControl.RollbackAction = () => {
                    _userInfoDbContext.RollbackTransaction();
                };
            }
        }
    }

    internal class TestWebSocketController : WebSocketController
    {
        public override async Task OnConnected(WebSocket webSocket)
        {
            Task.Run(async () => {
                Thread.Sleep(1000);
                await webSocket.SendString("hello");
            });
            var ret = await webSocket.ReadString();
            await webSocket.SendString(ret);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "abc", System.Threading.CancellationToken.None);
        }
    }
}
