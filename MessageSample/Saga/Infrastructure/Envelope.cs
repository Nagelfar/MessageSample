using System.Globalization;
using RabbitMQ.Client;

namespace MessageSample.Saga;

public interface IHandleMessageEnvelope<T> : IHandleMessage<Envelope<T>>
    where T : notnull
{
}

public static class Headers
{
    public const string CorrelationId = "CorrelationId";
    public const string CausationId = "CausationId";
    public const string MessageId = "MessageId";
    public const string SentAt = "SentAt";
}

public class Envelope
{
    protected Envelope(Type type, object body)
    {
        Type = type;
        Body = body;
    }

    private static string NewId() => Guid.NewGuid().ToString();

    public static Envelope Create(object content, IDictionary<string, string> metadata)
    {
        var genericType = typeof(Envelope<>).MakeGenericType(content.GetType());
        var envelope = (Envelope)Activator.CreateInstance(genericType, content)!;
        envelope.Metadata = metadata;
        return envelope;
    }

    public static Envelope Create<T>(T content, string? correlationId = null) where T : notnull
    {
        return new Envelope<T>(content)
        {
            Metadata =
            {
                [Headers.CorrelationId] = correlationId ?? NewId(),
                [Headers.MessageId] = NewId(),
                [Headers.SentAt] = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    public static Envelope Create<T>(T content, IDictionary<string, string> existingMetadata) where T : notnull
    {
        return new Envelope<T>(content)
        {
            Metadata = new Dictionary<string, string>(existingMetadata)
            {
                [Headers.CorrelationId] = GetValueOrNewId(Headers.CorrelationId),
                [Headers.CausationId] = GetValueOrNewId(Headers.MessageId),
                [Headers.MessageId] = NewId(),
                [Headers.SentAt] = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
            }
        };

        string GetValueOrNewId(string key)
        {
            return existingMetadata.TryGetValue(key, out var value) ? value : NewId();
        }
    }

    public Type Type { get; }
    public object Body { get; }
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

public class Envelope<T> : Envelope where T : notnull
{
    public Envelope(T body) : base(body.GetType(), body)
    {
        Body = body;
    }

    public new T Body { get; }
}

public static class EnvelopeExtensions
{
    public static Envelope CorrelateWith<T>(this Envelope envelope, T nextMessage)
    {
        return Envelope.Create(nextMessage, envelope.Metadata);
    }
    public static void Send(this IModel model, string queue, Envelope envelope)
    {
        var properties = model.CreateBasicProperties();
        properties.Type = envelope.Type.FullName;
        properties.CorrelationId = envelope.Metadata[Headers.CorrelationId];
        properties.MessageId = envelope.Metadata[Headers.MessageId];
        properties.Headers = envelope.Metadata.ToDictionary(x => x.Key, x => (object)x.Value);
        model.BasicPublish("", queue,
            basicProperties: properties,
            body: envelope.Body.Serialize());
    }
    public static void Send(this IModel model, string queue, IEnumerable<Envelope> envelopes)
    {
        var batch = model.CreateBasicPublishBatch();
        foreach (var envelope in envelopes)
        {
            var properties = model.CreateBasicProperties();
            properties.Type = envelope.Type.FullName;
            properties.CorrelationId = envelope.Metadata[Headers.CorrelationId];
            properties.MessageId = envelope.Metadata[Headers.MessageId];
            properties.Headers = envelope.Metadata.ToDictionary(x => x.Key, x => (object)x.Value);
            batch.Add("", queue, false, properties, envelope.Body.Serialize());
        }
        batch.Publish();
    }
}