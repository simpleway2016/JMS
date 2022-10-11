using JMS;
using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Way.Lib;

public class MicroServiceControllerBase
{
    internal class ThreadLocalObject
    {
        public InvokeCommand Command;
        public IServiceProvider ServiceProvider;
        internal ThreadLocalObject(InvokeCommand command, IServiceProvider serviceProvider)
        {
            Command = command;
            ServiceProvider = serviceProvider;
        }
    }
    internal IKeyLocker _keyLocker;
    internal NetClient NetClient;
    internal static ThreadLocal<ThreadLocalObject> RequestingObject = new ThreadLocal<ThreadLocalObject>();


    private IDictionary<string, string> _Header;
    /// <summary>
    /// 请求的头
    /// </summary>
    public IDictionary<string, string> Header
    {
        get
        {
            if (_Header == null && RequestingObject.Value != null)
            {
                _Header = RequestingObject.Value.Command.Header;
            }
            return _Header;
        }
    }

    /// <summary>
    /// 身份验证后获取的身份信息
    /// </summary>
    public object UserContent { get; internal set; }

    /// <summary>
    /// 当前的事务委托器
    /// </summary>
    public TransactionDelegate TransactionControl { set; get; }

    IServiceProvider _ServiceProvider;
    /// <summary>
    /// Controller的依赖注入服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider
    {
        get
        {
            if (_ServiceProvider == null && RequestingObject.Value != null)
            {
                _ServiceProvider = RequestingObject.Value.ServiceProvider;
            }
            return _ServiceProvider;
        }
    }

    string _transactionid;
    /// <summary>
    /// 事务id
    /// </summary>
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
    public virtual bool TryLock(string key)
    {
        return _keyLocker.TryLock(this.TransactionId, key);
    }
    /// <summary>
    /// 释放锁定的key
    /// </summary>
    /// <param name="key"></param>
    public virtual bool TryUnLock(string key)
    {
        return _keyLocker.TryUnLock(this.TransactionId, key);
    }

    /// <summary>
    /// 当调用Controller发生异常时触发的函数
    /// </summary>
    /// <param name="actionName">调用的方法名称</param>
    /// <param name="parameters">传入的参数</param>
    /// <param name="error">异常</param>
    /// <returns>true 表示无需记录错误日志</returns>
    public virtual bool OnInvokeError(string actionName, object[] parameters, Exception error)
    {
        return false;
    }

    public virtual void OnBeforeAction(string actionName, object[] parameters)
    {

    }
    public virtual void OnAfterAction(string actionName, object[] parameters)
    {

    }

    public virtual void OnUnLoad()
    {

    }
}

public class ControllerTypeInfo
{
    public string ServiceName;
    public Type Type;
    public TypeMethodInfo[] Methods;
    public bool Enable;
    /// <summary>
    /// 是否需要身份验证
    /// </summary>
    public bool NeedAuthorize = false;
}
public class TypeMethodInfo
{
    public MethodInfo Method;
    public bool NeedAuthorize;
}
