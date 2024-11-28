using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JMS.Dtos
{
    [JsonSerializable(typeof(UserInfo))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
