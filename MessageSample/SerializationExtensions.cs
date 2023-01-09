using System.Text;
using System.Text.Json;

namespace MessageSample;

public static class SerializationExtensions
{
    public static byte[] Serialize<T>(this T value)
    {
        var serialized = JsonSerializer.Serialize(value);
        return Encoding.UTF8.GetBytes(serialized);
    }

    public static T? TryDeserialize<T>(this ReadOnlySpan<byte> bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static T Deserialize<T>(this ReadOnlySpan<byte> bytes)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(bytes);
            if (result is null)
                throw new Exception(
                    $"Could not deserialize {Encoding.UTF8.GetString(bytes)} into {typeof(T).FullName}");
            return result;
        }
        catch (JsonException e)
        {
            var content = Encoding.UTF8.GetString(bytes);
            throw new JsonException($"Could not deserialize {content}", e);
        }
    }

    public static object? Deserialize(this ReadOnlySpan<byte> bytes, Type type)
    {
        return JsonSerializer.Deserialize(bytes, type);
    }
}

