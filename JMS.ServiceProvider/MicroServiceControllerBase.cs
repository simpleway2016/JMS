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

    string _transactionid;
    public string TransactionId
    {
        get
        {
            return _transactionid ??= this.Header["TranId"];
        }
    }

    internal static ThreadLocal<MicroServiceControllerBase> ThreadCurrent = new ThreadLocal<MicroServiceControllerBase>();
    /// <summary>
    /// 与当前请求相关联的Controller对象
    /// </summary>
    public static MicroServiceControllerBase Current
    {
        get
        {
            return ThreadCurrent.Value;
        }
        internal set
        {
            ThreadCurrent.Value = value;
        }
    }

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
    /// 申请锁住指定的key，使用完务必保证调用UnLock释放锁定的key
    /// </summary>
    /// <param name="key"></param>
    /// <returns>是否成功</returns>
    public bool TryLock(string key)
    {
        return _keyLocker.TryLock( this.TransactionId, key);
    }
    /// <summary>
    /// 释放锁定的key
    /// </summary>
    /// <param name="key"></param>
    public bool TryUnLock(string key)
    {
        return _keyLocker.TryUnLock(this.TransactionId, key);
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
    public bool Enable;
}
