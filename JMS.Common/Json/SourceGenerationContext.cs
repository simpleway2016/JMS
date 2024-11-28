using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JMS.Common.Json
{
    [JsonSerializable(typeof(ClientServiceDetail))]
    [JsonSerializable(typeof(GatewayCommand))]
    [JsonSerializable(typeof(GetServiceProviderRequest))]
    [JsonSerializable(typeof(InvokeCommand))]
    [JsonSerializable(typeof(InvokeResult))]
    [JsonSerializable(typeof(LockKeyInfo))]
    [JsonSerializable(typeof(RegisterServiceLocation))]
    [JsonSerializable(typeof(RegisterServiceInfo))]
    [JsonSerializable(typeof(PerformanceInfo))]
    [JsonSerializable(typeof(RegisterServiceRunningInfo))]
    [JsonSerializable(typeof(ServiceDetail))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
