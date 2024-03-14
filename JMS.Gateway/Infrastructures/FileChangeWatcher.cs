﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace JMS.Infrastructures
{
    class FileChangeWatcher
    {
        IConfiguration _configuration;
        FileSystemWatcher _fw;
        ILogger<FileChangeWatcher> _logger;
        string _root;
        List<string> _changingFiles = new List<string>();
        AutoResetEvent _waitObj = new AutoResetEvent(false);
        public FileChangeWatcher(IConfiguration configuration,ILogger<FileChangeWatcher> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _root = configuration.GetValue<string>("ShareFolder");
            if (string.IsNullOrEmpty(_root))
                return;

            new Thread(timeBuffer).Start();

            _fw = new FileSystemWatcher(_root);           
            _fw.IncludeSubdirectories = true;
            _fw.Created += _fw_Changed;
            _fw.Changed += _fw_Changed;
            _fw.EnableRaisingEvents = true; //启动监控
        }

        void timeBuffer()
        {
            while(true)
            {
                try
                {
                    _waitObj.WaitOne();
                    Thread.Sleep(1000);
                    string[] files = null;
                    lock (_changingFiles)
                    {
                        files = _changingFiles.ToArray();
                        _changingFiles.Clear();
                    }

                    foreach(var file in files )
                    {
                        _logger.LogInformation("{0} changed", file);
                        SystemEventCenter.OnShareFileChanged(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }

        private void _fw_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
                return;
           var path =  Path.GetRelativePath(_root, e.FullPath).Replace("\\", "/");            

            lock(_changingFiles)
            {
                if(_changingFiles.Contains(path) == false)
                {
                    _logger?.LogInformation("{0}更新", path);
                    _changingFiles.Add(path);
                }
            }
            _waitObj.Set();
        }
    }

}
