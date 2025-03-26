using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy
{
    class WriteLogger
    {
        static FileStream _fs;
        static long _memory;
        static WriteLogger()
        {
            _fs = File.Create("aaa.csv");
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var p = Process.GetCurrentProcess();
                        _memory = p.WorkingSet64;
                    }
                    catch  
                    {
                         
                    }
                    Thread.Sleep(1000);
                }
            }).Start();

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        bool toflush = false;
                        while (_datas.TryDequeue(out infoitem item))
                        {
                            _fs.Write(Encoding.UTF8.GetBytes($"{DateTime.Now.ToString("MM-dd HH:mm:ss")},{item.content},{item.mem}\r\n"));
                            toflush = true;
                        }
                        if (toflush)
                        {
                            _fs.Flush();
                        }
                    }
                    catch
                    {

                    }
                    Thread.Sleep(1000);
                }
            }).Start();
        }

        static ConcurrentQueue<infoitem> _datas = new ConcurrentQueue<infoitem>();

        public static void Write(string content)
        {
            _datas.Enqueue(new infoitem { content = content, mem = _memory });
        }
    }
    class infoitem
    {
        public string content;
        public long mem;
    }
}
