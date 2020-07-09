using JMS;
using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Way.Lib;

public class MicroServiceControllerBase
{
    internal IKeyLocker _keyLocker;
    internal NetClient NetClient;
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

    /// <summary>
    /// 申请锁住指定的key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="waitToSuccess">一直等到成功为止</param>
    /// <returns>是否成功</returns>
    public bool TryLock(string key,bool waitToSuccess)
    {
        return _keyLocker.TryLock(key , waitToSuccess);
    }

    public void UnLock(string key)
    {
        _keyLocker.UnLock(key);
    }

    public virtual void InvokeError(string actionName, object[] parameters,Exception error)
    {

    }

    public virtual void BeforeAction(string actionName, object[] parameters)
    {

    }
    public virtual void AfterAction(string actionName, object[] parameters)
    {

    }

    public virtual void UnLoad()
    {

    }
}

class ControllerTypeInfo
{
    public Type Type;
    public MethodInfo[] Methods;
}
