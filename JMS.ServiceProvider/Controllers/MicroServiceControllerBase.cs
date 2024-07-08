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
using System.Security.Claims;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using JMS.ServerCore.Http;

public class MicroServiceControllerBase : BaseJmsController, IDisposable
{


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
            if (_transactionid == null)
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

    public virtual void Dispose()
    {

    }

    /// <summary>
    /// 返回执行成功的HttpResult
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    protected virtual HttpResult Ok(object data)
    {
        return new HttpResult(200, data);
    }

    /// <summary>
    /// 返回执行失败的HttpResult，事务也会被自动回滚
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    protected virtual HttpResult Error(string errorMessage)
    {
        return new HttpResult(500, errorMessage);
    }
}





