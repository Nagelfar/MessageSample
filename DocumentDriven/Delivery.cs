using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.DocumentDriven;

public class Delivery : IDisposable, IHostedService
{
    private readonly ILogger<Delivery> _logger;
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;
    private static ConcurrentDictionary<int, OrderDocument> _orders = new ConcurrentDictionary<int, OrderDocument>();

    public Delivery(IConnection connection, ILogger<Delivery> logger)
    {
        _logger = logger;
        _model = connection.CreateModel();
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        var message = ea.Body.Span.Deserialize<OrderDocument>();
        _logger.LogInformation("DocumentDriven: Received order {@Message}", message);
        if (_orders.TryGetValue(message.Order, out var order))
        {
            if (order.DeliveredFood != message.CookedFood)
            {
                _logger.LogInformation("DocumentDriven: Delivering Cooked Food for {@FoodCooked}", message);
                _orders.AddOrUpdate(message.Order, _ => message, (_, existing) =>
                    {
                        existing.DeliveredFood = message.CookedFood;
                        return existing;
                    }
                );
            }
        }
        else
        {
            if (message.OrderedDrink.Any())
                _logger.LogInformation("DocumentDriven: Delivering drinks {@Drink}", message.OrderedDrink);
            _orders.AddOrUpdate(message.Order, _ => message, (_, existing) =>
                message);
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