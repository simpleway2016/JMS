using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JMS.Common.Json
{
    public interface IJsonSerializer
    {
        JsonSerializerOptions SerializerOptions { get; }
        string Serialize(object value);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writeIndented">defines whether JSON should use pretty printing.</param>
        /// <returns></returns>
        string Serialize(object value, bool writeIndented);
        T Deserialize<T>(string jsonString);
        object Deserialize(string jsonString,Type targetType);
    }

    class DefaultJsonSerializer : IJsonSerializer
    {
        JsonSerializerOptions _SerializerOptions ;

        public JsonSerializerOptions SerializerOptions => _SerializerOptions;

        public DefaultJsonSerializer()
        {
            _SerializerOptions = new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,//不区分大小写,为了兼容之前Newtonsoft.Json的代码
            };
            _SerializerOptions.Converters.Add(new StringJsonConverter());
            _SerializerOptions.Converters.Add(new TypeJsonConverter());

            //这些转换器的作用是为了让long、ulong、double在序列化时，超过JS安全整数范围时，转为字符串
            _SerializerOptions.Converters.Add(new LongJsonConverter());
            _SerializerOptions.Converters.Add(new UlongJsonConverter());
            _SerializerOptions.Converters.Add(new DoubleJsonConverter());
            _SerializerOptions.Converters.Add(new DecimalJsonConverter());

            //因为SourceGenerationContext不支持InvokeResult<>泛型，所以，不能让SourceGenerationContext来解析类型
            // _SerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        }


        public T Deserialize<T>(string jsonString)
        {
            if(string.IsNullOrEmpty(jsonString))
                return default(T);
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonString, _SerializerOptions);
        }

        public object Deserialize(string jsonString, Type targetType)
        {
            if (string.IsNullOrEmpty(jsonString))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize(jsonString, targetType, _SerializerOptions);
        }

        public string Serialize(object value)
        {
            if (value == null)
                return null;
            return System.Text.Json.JsonSerializer.Serialize(value, _SerializerOptions);
        }

        public string Serialize(object value, bool writeIndented)
        {
            if (value == null)
                return null;
            if (writeIndented)
            {
                JsonSerializerOptions options = new JsonSerializerOptions(_SerializerOptions);
                options.WriteIndented = writeIndented;

                return System.Text.Json.JsonSerializer.Serialize(value, options);
            }
            return System.Text.Json.JsonSerializer.Serialize(value, _SerializerOptions);
        }
    }

    class DecimalJsonConverter : JsonConverter<decimal>
    {



        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return decimal.Parse(reader.GetString());
                case JsonTokenType.Number:
                    return reader.GetDecimal();
                case JsonTokenType.True:
                    return 1;

                case JsonTokenType.False:
                    return 0;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            // 写入字符串值
            if (value <= 90071992547409)
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                //js会有精度丢失，所以，转为字符串
                writer.WriteStringValue(value.ToString());
            }

        }
    }
    class DoubleJsonConverter : JsonConverter<double>
    {



        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return double.Parse(reader.GetString());
                case JsonTokenType.Number:
                    return reader.GetDouble();
                case JsonTokenType.True:
                    return 1;

                case JsonTokenType.False:
                    return 0;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            // 写入字符串值
            if (value <= 90071992547409)
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                //js会有精度丢失，所以，转为字符串
                writer.WriteStringValue(value.ToString());
            }

        }
    }
    class LongJsonConverter : JsonConverter<long>
    {


     
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return long.Parse( reader.GetString());
                case JsonTokenType.Number:
                    return reader.GetInt64();
                case JsonTokenType.True:
                    return 1;

                case JsonTokenType.False:
                    return 0;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {           
            // 写入字符串值
            if(value <= 9007199254740991L)
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                //js会有精度丢失，所以，转为字符串
                writer.WriteStringValue(value.ToString());
            }
               
        }
    }
    class UlongJsonConverter : JsonConverter<ulong>
    {



        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return ulong.Parse(reader.GetString());
                case JsonTokenType.Number:
                    return reader.GetUInt64();
                case JsonTokenType.True:
                    return 1;

                case JsonTokenType.False:
                    return 0;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
        {
            // 写入字符串值
            if (value <= 9007199254740991L)
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                //js会有精度丢失，所以，转为字符串
                writer.WriteStringValue(value.ToString());
            }

        }
    }

    class StringJsonConverter : JsonConverter<string>
    {
        void readToEndObject(ref Utf8JsonReader reader)
        {
            while (true)
            {
                if (reader.Read() == false)
                    return;

                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return;
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    readToEndObject(ref reader);
                }
            }
        }
        void readToEndArray(ref Utf8JsonReader reader)
        {
            while (true)
            {
                if (reader.Read() == false)
                    return;

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return;
                }

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    readToEndArray(ref reader);
                }
            }
        }
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 根据 JsonTokenType 处理不同类型的输入
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                   return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
                case JsonTokenType.True:
                    return "true";

                case JsonTokenType.False:
                    return "false";

                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.StartObject:
                    readToEndObject(ref reader);
                    return null;
                case JsonTokenType.StartArray:
                    readToEndArray(ref reader);
                    return null;
                default:
                    return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            // 如果值为 null，写入 null
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // 写入字符串值
            writer.WriteStringValue(value);
        }
    }

    public class TypeJsonConverter : JsonConverter<Type>
    {
        public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected string value for Type");
            }

            string typeName = reader.GetString();
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            try
            {
                // 尝试多种方式加载类型
                Type type = Type.GetType(typeName);
                if (type != null)
                {
                    return type;
                }

                // 如果上面的方法失败，尝试从所有已加载的程序集中查找
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        return type;
                    }
                }

                throw new JsonException($"Could not find type: {typeName}");
            }
            catch (Exception ex)
            {
                throw new JsonException($"Error converting type name '{typeName}': {ex.Message}", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.FullName);
        }
    }
}
