using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.TransactionReporters
{
    internal static class TransactionReporterRoute
    {
        internal static ILogger<RemoteClient> Logger;
        static GatewayReporter _GatewayReporter;
        static LocationFileReporter _LocationFileReporter;
        static TransactionReporterRoute()
        {
            _GatewayReporter = new GatewayReporter();
            _LocationFileReporter = new LocationFileReporter();
        }

        public static ITransactionReporter GetReporter(RemoteClient remoteClient)
        {
            //如果有一个服务不是网关分配，那么，不向网关报告情况
            bool reportToGateway = !remoteClient._Connects.Any(m => m.Invoker.IsFromGateway == false || m.InvokingInfo.ServiceLocation.Port == 0);

            if (reportToGateway)
                return _GatewayReporter;
            else
                return _LocationFileReporter;
        }
    }
}
