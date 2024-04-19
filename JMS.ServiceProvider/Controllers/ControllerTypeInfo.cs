using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JMS.Controllers
{
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
        public List<AuthorizeAttribute> AuthorizeAttributes = new List<AuthorizeAttribute>();
        public TypeParameterInfo[] Parameters;
        public TypeMethodInfo(MethodInfo method, Type controllerType)
        {
            Method = method;

            AuthorizeAttributes.AddRange(method.GetCustomAttributes<AuthorizeAttribute>());
            AuthorizeAttributes.AddRange(controllerType.GetCustomAttributes<AuthorizeAttribute>());

            Parameters = method.GetParameters().Select(m => new TypeParameterInfo(m)).ToArray();
        }
    }

    public class TypeParameterInfo
    {
        /// <summary>
        /// 是否需要进行数据验证
        /// </summary>
        public bool IsRequiredValidation { get; }
        public bool IsTransactionDelegate { get; }
        public TypeParameterInfo(ParameterInfo parameterInfo)
        {
            ParameterInfo = parameterInfo;
            IsTransactionDelegate = parameterInfo.ParameterType == typeof(TransactionDelegate);

            if (parameterInfo.ParameterType.IsValueType == false)
            {
                var properties = parameterInfo.ParameterType.GetProperties();
                foreach (var pro in properties)
                {
                    var attrs = pro.GetCustomAttributes();
                    foreach (var attr in attrs)
                    {
                        if (attr.GetType().IsSubclassOf(typeof(ValidationAttribute)))
                        {
                            IsRequiredValidation = true;
                            return;
                        }
                    }
                }
            }
        }

        public ParameterInfo ParameterInfo { get; }
    }

    public class ModelValidationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
