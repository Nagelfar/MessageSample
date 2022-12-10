using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.EventDriven;

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
        if (ea.Body.Span.TryDeserialize<OrderPlaced>() is {  } orderPlaced && orderPlaced.Drink.Any())
        {
            _logger.LogInformation("EventDriven: Recorded order for delivery {@Message}", orderPlaced);
            if (orderPlaced.Drink.Any())
                _logger.LogInformation("EventDriven: Delivering drinks {@Drink}", orderPlaced.Drink);
        }
        else if (ea.Body.Span.TryDeserialize<FoodCooked>() is { } foodCooked)
        {
            _logger.LogInformation("EventDriven: Delivering Cooked Food for {@FoodCooked}", foodCooked);
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
        _model.BasicConsume(queue: Topology.DeliverySubscription,
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