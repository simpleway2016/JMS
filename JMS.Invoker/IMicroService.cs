using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IMicroService
    {
        void Invoke(string method, params object[] parameters);
        T Invoke<T>(string method, params object[] parameters);
        Task<T> InvokeAsync<T>(string method, params object[] parameters);
        Task InvokeAsync(string method, params object[] parameters);
    }
}
