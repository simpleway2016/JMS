using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS
{
    public static class Extentions
    {
        public static string[] GetStringArrayParameters(this object[] source)
        {
            if (source == null)
                return null;

            string[] ret = new string[source.Length];
            for(int i = 0; i < source.Length; i ++)
            {
                ret[i] = source[i].ToJsonString();
            }
            return ret;
        }
        public static string ReadServiceData(this Way.Lib.NetStream  netclient)
        {
            var len = netclient.ReadInt();
            var data = netclient.ReceiveDatas(len);
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public static T ReadServiceObject<T>(this Way.Lib.NetStream netclient)
        {
            var len = netclient.ReadInt();
            var datas = netclient.ReceiveDatas(len);
            string str = Encoding.UTF8.GetString(datas);
            try
            {
                return str.FromJson<T>();
            }
            catch (Exception ex)
            {
                throw new ConvertException( str , $"无法将{str}实例化为{typeof(T).FullName}");
            }
           
        }

        public static void WriteServiceData(this Way.Lib.NetStream netclient, byte[] data)
        {
            netclient.Write(data.Length);
            netclient.Write(data);
        }
        public static void WriteServiceData(this Way.Lib.NetStream netclient, object value)
        {
            if (value == null)
            {
                Extentions.WriteServiceData(netclient,new byte[0]);
            }
            else
            {
                Extentions.WriteServiceData(netclient, Encoding.UTF8.GetBytes(value.ToJsonString()));
            }

        }
    }
}
