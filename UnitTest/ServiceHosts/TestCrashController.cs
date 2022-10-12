using JMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal class TestCrashController : MicroServiceControllerBase
    {
        public static string FinallyText;
        public static bool CanCrash = true;

        string _text;
        public void SetText(string text)
        {
            _text = text;
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);

            this.TransactionControl = new JMS.TransactionDelegate(this.TransactionId);
            this.TransactionControl.CommitAction = () => {
                if (CanCrash)
                {
                    CanCrash = false;
                    throw new Exception("故意宕机");
                }

                FinallyText = _text;
            };
            this.TransactionControl.RollbackAction = () => { 
                
            };
        }
    }
}
