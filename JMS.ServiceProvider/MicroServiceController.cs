using JMS;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Way.Lib;

public class MicroServiceController
{
    internal Way.Lib.NetStream NetClient;
    internal static ThreadLocal<InvokeCommand> RequestingCommand = new ThreadLocal<InvokeCommand>();


    private IDictionary<string,string> _Header;
    public IDictionary<string,string> Header
    {
        get
        {
            if(_Header == null)
            {
                _Header = RequestingCommand.Value.Header;
            }
            return _Header;
        }
    }
    public TransactionDelegate TransactionControl { set; get; }

    /// <summary>
    /// 终止请求，并返回指定的值
    /// </summary>
    /// <param name="returnValue">指定返回的值</param>
    protected void ResponseEnd(object returnValue)
    {
        NetClient.WriteServiceData(new InvokeResult
        {
            Success = true,
            SupportTransaction = false,
            Data = returnValue

        });
        throw new ResponseEndException();
    }

    public virtual void BeforeAction(string actionName, object[] parameters)
    {

    }
    public virtual void AfterAction(string actionName, object[] parameters)
    {

    }
}
