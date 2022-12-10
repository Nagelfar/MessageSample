using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDriven;

public class Delivery : IDisposable, IHostedService
{
    private readonly ILogger<Delivery> _logger;
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;

    public Delivery(IConnection connection, ILogger<Delivery> logger)
    {
        _logger = logger;
        _model = connection.CreateModel();
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        if (ea.Body.Span.TryDeserialize<DeliverItems>() is { } deliver && deliver.Drinks.Any())
            _logger.LogInformation("CommandDriven: Delivering Order Items for {@Message}", deliver);
        else if (ea.Body.Span.TryDeserialize<DeliverCookedFood>() is { } cooked)
        {
            _logger.LogInformation("CommandDriven: Delivering Cooked Food for {@Message}", cooked);
        }

        _model.BasicAck(ea.DeliveryTag, false);
    }

    public void Dispose()
    {
        _model.Abort();
        _model.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _model.BasicConsume(queue: Topology.DeliveryQueue,
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