using JMS;
using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Way.Lib;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

public class MicroServiceControllerBase: BaseJmsController
{
    internal class LocalObject
    {
        public InvokeCommand Command;
        public IServiceProvider ServiceProvider;
        public object UserContent;
        public string RequestPath;
        public EndPoint RemoteEndPoint;
        internal LocalObject(EndPoint remoteEndPoint, InvokeCommand command, IServiceProvider serviceProvider,object userContent)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Command = command;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
        }

        internal LocalObject(EndPoint remoteEndPoint, InvokeCommand command, IServiceProvider serviceProvider, object userContent,string requestPath)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Command = command;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
            this.RequestPath = requestPath;
        }
    }
    internal IKeyLocker _keyLocker;
    internal NetClient NetClient;
   



    /// <summary>
    /// 当前的事务委托器
    /// </summary>
    public TransactionDelegate TransactionControl { set; get; }



    string _transactionid;
    /// <summary>
    /// 事务id
    /// </summary>
    public string TransactionId
    {
        get
        {
            if(_transactionid == null)
            {
                _transactionid = this.Headers["TranId"];
            }
            return _transactionid;
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
    public ServiceDetail Service;
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
    public bool AllowAnonymous;
}
