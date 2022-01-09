using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit;

namespace OrgAnalyzer;

public class StringEnumJsonConverter<TEnum> : JsonConverter<StringEnum<TEnum>> where TEnum: struct, Enum
{
    public override StringEnum<TEnum> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new StringEnum<TEnum>(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, StringEnum<TEnum> value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.StringValue);
    }
}
