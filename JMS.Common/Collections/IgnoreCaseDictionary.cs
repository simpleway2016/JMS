using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common.Collections
{
    /// <summary>
    /// 忽略大小写的Dictionary
    /// </summary>
    public class IgnoreCaseDictionary : Dictionary<string, object>
    {
        public IgnoreCaseDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue">为null时，返回的默认值</param>
        /// <returns></returns>
        public T TryGetValue<T>(string key, T defaultValue = default(T))
        {
            if (this.ContainsKey(key))
            {
                return GetValue<T>(key);
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGetValue<T>(string key, out T result)
        {
            if (this.ContainsKey(key))
            {
                result = GetValue<T>(key);
                return true;
            }
            else
            {
                result = default(T);
                return false;
            }
        }

        public T GetValue<T>(string key)
        {
            var dictval = this[key];
            if (dictval == null)
                return default(T);

            var targetType = typeof(T);
            if (targetType.IsEnum)
            {
                if (dictval is string)
                {
                    var obj = Enum.Parse(targetType, (string)dictval);
                    return (T)obj;
                }
                object val = Convert.ToInt32(dictval);
                return (T)val;
            }
            else if (targetType.IsValueType && targetType.IsGenericType)
            {
                var types = targetType.GetGenericArguments();
                if (types[0].IsEnum)
                {
                    var obj = Enum.Parse(types[0], dictval.ToString());
                    return (T)obj;
                }
                return (T)Convert.ChangeType(dictval, types[0]);
            }
            else if (targetType.IsValueType)
            {
                return (T)Convert.ChangeType(dictval, typeof(T));
            }
            else
            {
                return (T)dictval;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public object TryGetValue(string key, object defaultValue = null)
        {
            if (this.ContainsKey(key))
            {
                return this[key];
            }
            else
            {
                return defaultValue;
            }
        }

    }
}