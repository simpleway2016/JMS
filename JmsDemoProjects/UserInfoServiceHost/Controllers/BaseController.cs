
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace UserInfoServiceHost.Controllers
{
    public class BaseController : MicroServiceControllerBase
    {
        SystemDBContext _CurrentDBContext;
        /// <summary>
        /// 当前线程的DBContext对象
        /// </summary>
        public SystemDBContext CurrentDBContext
        {
            get
            {
                return _CurrentDBContext ??= this.ServiceProvider.GetService<SystemDBContext>();
            }
        }

        public override bool OnInvokeError(string actionName, object[] parameters, Exception error)
        {
            base.OnInvokeError(actionName, parameters, error);

            if (error is ServiceException)
                return true; // return true表示不用生成日志

            return false;
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);

            if (CurrentDBContext.CurrentTransaction != null)
            {
                this.TransactionControl = new JMS.TransactionDelegate(this, CurrentDBContext);
            }
        }

    }
}
