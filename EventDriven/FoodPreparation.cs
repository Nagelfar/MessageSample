using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.EventDriven;

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
        var deserialized = JsonSerializer.Deserialize<OrderPlaced>(ea.Body.Span);
        _logger.LogInformation("EventDriven: Received order for {@Message}", deserialized);
        if (deserialized.Food.Any())
        {
            _logger.LogInformation("EventDriven: Will start cooking {@Food}", deserialized.Food);
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
        _model.BasicConsume(queue: Topology.FoodPreparationSubscription,
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