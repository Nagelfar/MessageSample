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
        _model.BasicQos(0,1,false);
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        try
        {
            _model.TxSelect();
            var deserialized = JsonSerializer.Deserialize<OrderPlaced>(ea.Body.Span);
            _logger.LogInformation("EventDriven: Received order for {@Message}", deserialized);
            if (deserialized.Food.Any())
            {
                _logger.LogInformation("EventDriven: Will start cooking {@Food}", deserialized.Food);
                Thread.Sleep(1000);
                _logger.LogInformation("EventDriven: Finished cooking {@Food}", deserialized.Food);

                var foodCookedEvents =
                    deserialized.Food
                        .Select(food => new FoodCooked
                        {
                            Food = food,
                            Order = deserialized.Order
                        })
                        .ToArray();
                foreach (var foodCookedEvent in foodCookedEvents)
                {
                    _model.BasicPublish(
                        Topology.FoodPreparationTopic,
                        routingKey: "",
                        body: foodCookedEvent.Serialize(),
                        mandatory: true);
                }
            }

            _model.BasicAck(ea.DeliveryTag, false);
            _model.TxCommit();
        }
        catch
        {
            _model.TxRollback();
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