using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaya.ScreenTranslator.Impl.Shared.Converters;

public sealed class SettingsJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, object>();
        if (reader.TokenType != JsonTokenType.StartObject)
            return result;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var key = reader.GetString()!;
            reader.Read();

            var val = ReadValue(ref reader);
            if (val is not null)
                result[key] = val;
        }

        return result;
    }

    private static object? ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intVal))
                    return intVal;
                return reader.GetDouble();
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                {
                    var obj = new Dictionary<string, object>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            break;
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var propKey = reader.GetString()!;
                            reader.Read();
                            obj[propKey] = ReadValue(ref reader)!;
                        }
                    }
                    return obj;
                }
            case JsonTokenType.StartArray:
                {
                    var list = new List<object>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            break;
                        list.Add(ReadValue(ref reader)!);
                    }
                    return list;
                }
            default:
                return JsonSerializer.Deserialize<object>(ref reader);
        }
    }

    public override void Write(
        Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kv in value)
        {
            if (Models.ManagedSettingKeys.IsEphemeralHostKey(kv.Key) || IsNonSerializable(kv.Value))
                continue;
            writer.WritePropertyName(kv.Key);
            WriteValue(writer, kv.Value, options);
        }
        writer.WriteEndObject();
    }

    private static bool IsNonSerializable(object? value) =>
        value is IntPtr or UIntPtr or nint or nuint;

    private static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else if (value is bool b)
        {
            writer.WriteBooleanValue(b);
        }
        else if (value is int i)
        {
            writer.WriteNumberValue(i);
        }
        else if (value is long l)
        {
            writer.WriteNumberValue(l);
        }
        else if (value is double d)
        {
            writer.WriteNumberValue(d);
        }
        else if (value is float f)
        {
            writer.WriteNumberValue(f);
        }
        else if (value is decimal dec)
        {
            writer.WriteNumberValue(dec);
        }
        else if (value is string s)
        {
            writer.WriteStringValue(s);
        }
        else if (value is IntPtr or UIntPtr or nint or nuint)
        {
            // Should be filtered by callers; never persist handles.
            writer.WriteNullValue();
        }
        else if (value is Dictionary<string, object> dict)
        {
            writer.WriteStartObject();
            foreach (var kv in dict)
            {
                if (Models.ManagedSettingKeys.IsEphemeralHostKey(kv.Key) || IsNonSerializable(kv.Value))
                    continue;
                writer.WritePropertyName(kv.Key);
                WriteValue(writer, kv.Value, options);
            }
            writer.WriteEndObject();
        }
        else if (value is IReadOnlyDictionary<string, object> irdict)
        {
            writer.WriteStartObject();
            foreach (var kv in irdict)
            {
                if (Models.ManagedSettingKeys.IsEphemeralHostKey(kv.Key) || IsNonSerializable(kv.Value))
                    continue;
                writer.WritePropertyName(kv.Key);
                WriteValue(writer, kv.Value, options);
            }
            writer.WriteEndObject();
        }
        else if (value is System.Collections.IList list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
                WriteValue(writer, item, options);
            writer.WriteEndArray();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(Dictionary<string, object>);
    }
}
