using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.Saga;

public class FoodPreparationHandler : IHandleMessageEnvelope<CookFood>
{
    private readonly ILogger<FoodPreparationHandler> _logger;
    private readonly IModel _model;

    private static int counter = 0;

    public FoodPreparationHandler(ILogger<FoodPreparationHandler> logger, IConnection connection)
    {
        _logger = logger;
        _model = connection.CreateModel();
    }

    public void Message(Envelope<CookFood> message)
    {
        counter++;
        if (counter % 3 == 0)
            throw new Exception("Failing cook");
        _logger.LogInformation("CommandDrivenPipeline: Cooking Food for {@Command}", message.Body);
        Thread.Sleep(2000);
        _logger.LogInformation("CommandDrivenPipeline: Food was cooked for {@Message}", message.Body);
        var command = new DeliverCookedFood
        {
            Order = message.Body.Order,
            Food = message.Body.Food
        };
        _model.Send(Topology.DeliveryQueue, Envelope.Create(command, message.Metadata));
    }
}