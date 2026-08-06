using System.Text.Json;
using System.Text.Json.Serialization;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;

namespace Dorosak.Infrastructure.Serialization;

internal static class DorosakJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ResultJsonConverterFactory());
        return options;
    }

    private sealed class ResultJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type converterType = typeof(ResultJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]);
            return (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true)!;
        }
    }

    private sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>
    {
        public override Result<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            ResultEnvelope<T>? envelope = JsonSerializer.Deserialize<ResultEnvelope<T>>(ref reader, options);
            if (envelope is null)
            {
                throw new JsonException("A result payload cannot be null.");
            }

            return envelope.IsSuccess
                ? Result.Success(envelope.Value!)
                : Result.Failure<T>(envelope.Failure ?? throw new JsonException("A failure result requires an error."));
        }

        public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
        {
            var envelope = new ResultEnvelope<T>(
                value.IsSuccess,
                value.IsSuccess ? value.Value : default,
                value.Failure);
            JsonSerializer.Serialize(writer, envelope, options);
        }
    }

    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Value, ResultError? Failure);
}
