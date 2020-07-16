using JMS.Common.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        NetAddress _GatewayAddr = new NetAddress("127.0.0.1", 6900);
        Process _GatewayProcess;
        public UnitTest1()
        {
            //Æô¶¯Íø¹Ø
            if(File.Exists("../../../../JMS.Gateway/bin/Debug/netcoreapp3.1/JMS.Gateway.dll"))
            {
                _GatewayProcess = Way.Lib.Runner.OpenProcess("dotnet", "../../../../JMS.Gateway/bin/Debug/netcoreapp3.1/JMS.Gateway.dll port:" + _GatewayAddr.Port);
                string line = _GatewayProcess.StandardOutput.ReadLine();
                line = _GatewayProcess.StandardOutput.ReadLine();
            }
           
        }

        ~UnitTest1()
        {
            _GatewayProcess.Kill();
        }

        [TestMethod]
        public void TestMethod1()
        {

        }
    }
}
