using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    internal static class Extention
    {
        public static int? GetStatusCode(this InvokeResult<string> invokeResult)
        {
            if (invokeResult.Data == null)
                return null;
            else if (int.TryParse(invokeResult.Data, out int o))
                return o;

            return null;
        }
        public static int? GetStatusCode<T>(this InvokeResult<T> invokeResult)
        {
            if (invokeResult.Data == null)
                return null;
            else if (int.TryParse(invokeResult.Data.ToString(), out int o))
                return o;


            return null;
        }
    }
}
