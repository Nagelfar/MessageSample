using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDrivenPipeline;

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

public class EnvelopeBodyHandler<TNext> : IHandleMessage<Envelope>
    where TNext : notnull
{
    private readonly IHandleMessage<TNext> _next;

    public EnvelopeBodyHandler(IHandleMessage<TNext> next)
    {
        _next = next;
    }

    public void Message(Envelope message)
    {
        if (message.Body is TNext matching)
        {
            _next.Message(matching);
        }
    }
}

public class UpCastEnvelopeHandler<TNext> : IHandleMessage<Envelope>
    where TNext : notnull
{
    private readonly IHandleMessageEnvelope<TNext> _next;

    public UpCastEnvelopeHandler(IHandleMessageEnvelope<TNext> next)
    {
        _next = next;
    }

    public void Message(Envelope message)
    {
        if (message is Envelope<TNext> matching)
        {
            _next.Message(matching);
        }
    }
}

public class EnvelopeMatchingHandler : IHandleMessage<Envelope>
{
    private readonly ImmutableDictionary<Type, IHandleMessage<Envelope>> _handlers;

    public EnvelopeMatchingHandler(IEnumerable<IHandleMessage<Envelope>> handlers)
    {
        _handlers = handlers.ToImmutableDictionary(handler =>
        {
            var envelopeTypeFromHandler = handler.GetType().GetGenericArguments().First();
            if (envelopeTypeFromHandler.IsGenericType && envelopeTypeFromHandler.GetGenericTypeDefinition() == typeof(Envelope<>))
            {
                var messageType = envelopeTypeFromHandler.GetGenericArguments().First();
                return messageType;
            }

            return envelopeTypeFromHandler;
        });
    }

    public void Message(Envelope message)
    {
        var handler = _handlers[message.Type];
        handler.Message(message);
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

            var metadata = new Dictionary<string, string>
            {
                [Headers.MessageId] = message.BasicProperties.MessageId,
                [Headers.CorrelationId] = message.BasicProperties.CorrelationId,
                [Headers.SentAt] =
                    new DateTime(message.BasicProperties.Timestamp.UnixTime).ToString(CultureInfo.InvariantCulture)
            };
            if (message.BasicProperties.Headers != null &&
                message.BasicProperties.Headers.TryGetValue(Headers.CausationId, out var value) && value is byte[] bytes )
                metadata[Headers.CausationId] = Encoding.UTF8.GetString(bytes);
            var envelope = Envelope.Create(deserialized, metadata);
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

public class RetryHandler<T> : IHandleMessage<T> where T : notnull
{
    private readonly int _maxRetries;
    private readonly TimeSpan _wait;
    private readonly IHandleMessage<T> _next;
    private readonly ILogger<RetryHandler<T>> _logger;

    public RetryHandler(int maxRetries, TimeSpan wait, IHandleMessage<T> next, ILogger<RetryHandler<T>> logger)
    {
        _maxRetries = maxRetries;
        _wait = wait;
        _next = next;
        _logger = logger;
    }

    private void TryHandleMessage(T message, int remainingRetries)
    {
        try
        {
            _next.Message(message);
        }
        catch (Exception e)
        {
            if (remainingRetries > 0)
            {
                _logger.LogInformation(e, "Failed to execute {@Message} on retry {Retry} - will try again after {Wait}",
                    message, _maxRetries - remainingRetries, _wait);
                Thread.Sleep(_wait);
                TryHandleMessage(message, remainingRetries - 1);
            }
            else
            {
                _logger.LogWarning(e, "Failed to execute {@Message} after {Retries}", message, _maxRetries);
                throw;
            }
        }
    }

    public void Message(T message)
    {
        TryHandleMessage(message, _maxRetries);
    }
}

public class IdempotencyHandler<T> : IHandleMessage<T> where T : notnull
{
    private readonly IHandleMessage<T> _next;
    private readonly ILogger<IdempotencyHandler<T>> _logger;
    private readonly ConcurrentDictionary<int, T> _receivedMessages = new ConcurrentDictionary<int, T>();

    public IdempotencyHandler(IHandleMessage<T> next, ILogger<IdempotencyHandler<T>> logger)
    {
        _next = next;
        _logger = logger;
    }

    public void Message(T message)
    {
        var hash = message.GetHashCode();
        if (_receivedMessages.ContainsKey(hash))
        {
            _logger.LogInformation("Already received and processed {@Message} - will ignore it", message);
        }

        _next.Message(message);
        _receivedMessages.TryAdd(hash, message);
    }
}

public class LoggingMessageHandler<T> : IHandleMessage<T> where T : notnull
{
    private readonly IHandleMessage<T> _next;
    private readonly ILogger<LoggingMessageHandler<T>> _logger;

    public LoggingMessageHandler(IHandleMessage<T> next, ILogger<LoggingMessageHandler<T>> logger)
    {
        _next = next;
        _logger = logger;
    }

    public void Message(T message)
    {
        _logger.LogInformation("Received {@Message}", message);
        try
        {
            _next.Message(message);
            _logger.LogInformation("Successfully handle {@Message}", message);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to handle {@Message}", message);
            throw;
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