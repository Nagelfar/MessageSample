using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDriven;

public class FoodPreparation : IDisposable, IHostedService
{
    private readonly ILogger<FoodPreparation> _logger;
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;

    public FoodPreparation(IConnection connection, ILogger<FoodPreparation> logger)
    {
        _logger = logger;
        _model = connection.CreateModel();
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        var deserialized = ea.Body.Span.Deserialize<CookFood>();
        _logger.LogInformation("CommandDriven: Cooking Food for {@Message}", deserialized);
        Thread.Sleep(2000);
        _logger.LogInformation("CommandDriven: Food was cooked for {@Message}", deserialized);
        var command = new DeliverCookedFood
        {
            Order = deserialized.Order,
            Food = deserialized.Food
        };
        _model.BasicPublish(exchange: "", routingKey: Topology.DeliveryQueue, body: command.Serialize());
        _model.BasicAck(ea.DeliveryTag, false);
    }

    public void Dispose()
    {
        _model.Abort();
        _model.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _model.BasicConsume(queue: Topology.FoodPreparationQueue,
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