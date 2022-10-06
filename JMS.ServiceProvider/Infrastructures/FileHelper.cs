using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JMS.Infrastructures
{
    internal class FileHelper
    {
        /// <summary>
        /// 改变文件扩展名
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="newExt"></param>
        /// <returns>返回新的文件路径</returns>
        public static string ChangeFileExt(string filepath,string newExt)
        {
            var index = filepath.LastIndexOf(".");
            var newPath = filepath.Substring(0, index) + newExt;
            if (newPath == filepath)
                return filepath;
            File.Move(filepath, newPath);
            return newPath;
        }
    }
}
