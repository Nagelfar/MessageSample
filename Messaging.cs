using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample;

public interface IHandleMessage<in T> where T : notnull
{
    public void Message(T message);
}

public class DownCastHandler<TNext> : IHandleMessage<TNext>
    where TNext : notnull
{
    private readonly IHandleMessage<object> _next;

    public DownCastHandler(IHandleMessage<object> next)
    {
        _next = next;
    }

    public void Message(TNext message)
    {
        _next.Message(message);
    }
}

public class UpCastHandler<TNext> : IHandleMessage<object>
    where TNext : notnull
{
    private readonly IHandleMessage<TNext> _next;

    public UpCastHandler(IHandleMessage<TNext> next)
    {
        _next = next;
    }

    public void Message(object message)
    {
        if (message is TNext matching)
        {
            _next.Message(matching);
        }
    }
}

public class TypeMatchingHandler : IHandleMessage<Envelope>
{
    private readonly ImmutableDictionary<Type, IHandleMessage<object>> _handlers;

    public TypeMatchingHandler(IEnumerable<IHandleMessage<object>> handlers)
    {
        _handlers = handlers.ToImmutableDictionary(h => h.GetType().GetGenericArguments().First());
    }

    public void Message(Envelope message)
    {
        var handler = _handlers[message.Type];
        handler.Message(message.Body);
    }
}

public class EnvelopeHandler : IHandleMessage<BasicDeliverEventArgs>
{
    private readonly IHandleMessage<Envelope> _handler;

    public EnvelopeHandler(IHandleMessage<Envelope> handler)
    {
        _handler = handler;
    }

    public void Message(BasicDeliverEventArgs message)
    {
        try
        {
            if (message.BasicProperties == null || string.IsNullOrEmpty(message.BasicProperties.Type))
                throw new Exception("Expecting an envelope type");
            var type = Type.GetType(message.BasicProperties.Type);
            if (type is null)
                throw new Exception($"Provided type {message.BasicProperties.Type} is an invalid CLR Type");
            var deserialized = message.Body.Span.Deserialize(type);
            if (deserialized is null)
                throw new Exception("Could not deserialize ${type} from body");
            var envelope = new Envelope(type, deserialized);
            _handler.Message(envelope);
        }
        catch (Exception e)
        {
            var content = Encoding.UTF8.GetString(message.Body.Span);
            throw new Exception("Failed to handle message with body: " + content, e);
        }
    }
}

public class DeserializingHandler<T> : IHandleMessage<BasicDeliverEventArgs>
    where T : notnull
{
    private readonly IHandleMessage<T> _next;

    public DeserializingHandler(IHandleMessage<T> next)
    {
        _next = next;
    }

    public void Message(BasicDeliverEventArgs command)
    {
        try
        {
            var content = command.Body.Span.Deserialize<T>();
            _next.Message(content);
        }
        catch (Exception e)
        {
            var content = Encoding.UTF8.GetString(command.Body.Span);
            throw new Exception("Failed to deserialize: " + content, e);
        }
    }
}

public class RabbitMqEventHandler<TNeededToMakeTheHostUnique> : IHostedService, IDisposable
{
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;
    private readonly string _queue;
    private readonly IHandleMessage<BasicDeliverEventArgs> _next;

    public RabbitMqEventHandler(IConnection connection, string queue, IHandleMessage<BasicDeliverEventArgs> next)
    {
        _queue = queue;
        _next = next;
        _model = connection.CreateModel();
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (_, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        try
        {
            _next.Message(ea);
            _model.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            _model.BasicNack(ea.DeliveryTag, false, true);
            throw;
        }
    }

    public void Dispose()
    {
        _model.Abort();
        _model.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _model.BasicConsume(queue: _queue,
            autoAck: false,
            consumer: _consumer);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _model.Abort();
        return Task.CompletedTask;
    }
}

public class Envelope
{
    public Envelope(Type type, object body)
    {
        Type = type;
        Body = body;
    }

    public static Envelope Create<T>(T content) where T : notnull
    {
        return new Envelope(typeof(T), content);
    }

    public Type Type { get; }
    public object Body { get; }
}

public static class EnvelopeExtensions
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

    public static void Send(this IModel model, string queue, Envelope envelope)
    {
        var properties = model.CreateBasicProperties();
        properties.Type = envelope.Type.FullName;
        model.BasicPublish("", queue,
            basicProperties: properties,
            body: envelope.Body.Serialize());
    }
}