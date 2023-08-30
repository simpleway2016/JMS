using JMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UnitTest.ServiceHosts
{
    internal class TestCrashController : MicroServiceControllerBase,IStorageEngine
    {
        public static string FinallyText;
        public static bool CanCrash = true;

        string _text;

        public object CurrentTransaction => _text;

        public void SetText(string text)
        {
            _text = text;
            this.TransactionControl = new JMS.TransactionDelegate(this, this);
        }

        public void SetTextWithUserContent(string text)
        {
            if (CanCrash)
            {
                var claimsIdentity = new ClaimsIdentity(new Claim[]
                {
                new Claim("Content", text),
                new Claim(ClaimTypes.Role , "admin"),
          }, "JMS.Token"); ;

                var field = typeof(BaseJmsController).GetField("_userContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(this, new ClaimsPrincipal(claimsIdentity));
            }
            else
            {
                if (this.UserContent == null || this.UserContent.FindFirstValue(ClaimTypes.Role) != "admin")
                    throw new Exception("没有恢复身份");
            }
            SetText(text);
        }

        public async Task<string> NoTran(string text)
        {
            return text;
        }

        public void BeginTransaction()
        {
            
        }

        public void CommitTransaction()
        {
            if (CanCrash)
            {
                CanCrash = false;
                throw new Exception("故意宕机");
            }

            FinallyText = _text;
        }

        public void RollbackTransaction()
        {
            _text = null;
        }
    }
}
