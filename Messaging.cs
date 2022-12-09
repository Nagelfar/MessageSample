using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample;

public interface IHandleMessage<in T>
{
    public void Message(T message);
}

public class DeserializingHandler<T> : IHandleMessage<BasicDeliverEventArgs>
{
    private readonly IHandleMessage<T> _next;

    public DeserializingHandler(IHandleMessage<T> next)
    {
        _next = next;
    }

    public void Message(BasicDeliverEventArgs command)
    {
        var body = command.Body.ToArray();
        try
        {
            var content = JsonSerializer.Deserialize<T>(body);
            _next.Message(content);
        }
        catch (Exception e)
        {
            var content = Encoding.UTF8.GetString(body);
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
        _consumer.Received += (model, ea) => { OnMessage(ea); };
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
    public static Envelope Create<T>(T content)
    {
        return new Envelope
        {
            Body = JsonSerializer.SerializeToNode(content)!,
            Type = typeof(T).FullName!,
            SentAt = DateTime.Now
        };
    }

    public DateTime SentAt { get; init; }

    public string Type { get; init; }

    public JsonNode Body { get; init; }
}

public static class EnvelopeExtensions
{
    public static string Serialize(this Envelope envelope)
    {
        return JsonSerializer.Serialize(envelope);
    }

    public static Envelope Deserialize<T>(this string value)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(value);
            // if (envelope.Type == typeof(T).FullName)
            return envelope;
        }
        catch (Exception e)
        {
            return null;
        }
    }
}