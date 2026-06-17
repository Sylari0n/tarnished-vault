using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace TarnishedVault.Api.Services;

public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options) => _options = options;

    public override T FromStream<T>(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, _options)
               ?? throw new InvalidOperationException("Deserialization returned null");
    }

    public override Stream ToStream<T>(T input)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(input, _options);
        return new MemoryStream(bytes);
    }
}
