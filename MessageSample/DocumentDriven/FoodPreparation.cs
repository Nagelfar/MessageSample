using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.DocumentDriven;

public class FoodPreparation : IDisposable, IHostedService
{
    private readonly ILogger<FoodPreparation> _logger;
    private readonly FaultyCookImplementation _faultyCookImplementation;
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;

    private static ISet<int> _finishedOrders = new HashSet<int>();

    public FoodPreparation(IConnection connection, ILogger<FoodPreparation> logger,FaultyCookImplementation faultyCookImplementation)
    {
        _logger = logger;
        _faultyCookImplementation = faultyCookImplementation;
        _model = connection.CreateModel();
        _model.BasicQos(0,1,false);
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        var message = ea.Body.Span.Deserialize<OrderDocument>();
        _logger.LogInformation("DocumentDriven: Received order document for {@Message}", message);
        if (_finishedOrders.Contains(message.Order))
            _logger.LogInformation("DocumentDriven: Already processed order {Order} - ignoring it", message.Order);
        else
        {
            _logger.LogInformation("DocumentDriven: Will start cooking {@Food}", message.OrderedFood);
            _faultyCookImplementation.Operate();
            _logger.LogInformation("DocumentDriven: Finished cooking {@Food}", message.OrderedFood);
            var newOrder = message.Clone();
            newOrder.CookedFood = message.OrderedFood;
            _finishedOrders.Add(message.Order);
            _model.BasicPublish(Topology.OrdersTopic, "#", body: newOrder.Serialize());
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