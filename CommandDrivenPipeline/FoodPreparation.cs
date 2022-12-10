using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDrivenPipeline;

public class FoodPreparationHandler : IHandleMessage<CookFood>
{
    private readonly ILogger<FoodPreparationHandler> _logger;
    private readonly IModel _model;

    public FoodPreparationHandler(ILogger<FoodPreparationHandler> logger, IConnection connection)
    {
        _logger = logger;
        _model = connection.CreateModel();
    }

    public void Message(CookFood message)
    {
        _logger.LogInformation("CommandDrivenPipeline: Cooking Food for {@Command}", message);
        Thread.Sleep(2000);
        _logger.LogInformation("CommandDrivenPipeline: Food was cooked for {@Message}", message);
        var command = new DeliverCookedFood
        {
            Order = message.Order,
            Food = message.Food
        };
        _model.Send(Topology.DeliveryQueue, Envelope.Create(command));
    }
}