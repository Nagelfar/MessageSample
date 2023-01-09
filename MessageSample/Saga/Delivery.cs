using MessageSample.Saga;
using RabbitMQ.Client;

namespace MessageSample.Saga;

public class DeliveryHandler : IHandleMessageEnvelope<DeliverDrinks>, IHandleMessageEnvelope<DeliverCookedFood>
{
    private readonly ILogger<DeliveryHandler> _logger;
    private readonly IModel _model;

    public DeliveryHandler(ILogger<DeliveryHandler> logger, IConnection connection)
    {
        _logger = logger;
        _model = connection.CreateModel();
    }

    public void Message(Envelope<DeliverDrinks> command)
    {
        _logger.LogInformation("CommandDrivenPipeline: Deliver ordered items for {@Message}", command);
        _model.Publish(Topology.DeliveryTopic,
            command.CorrelateWith(new ItemsDelivered { DeliveryRequest = command.Body.DeliveryRequest }));
    }

    public void Message(Envelope<DeliverCookedFood> message)
    {
        _logger.LogInformation("CommandDrivenPipeline: Deliver Cooked Food for {@Message}", message);
        _model.Publish(Topology.DeliveryTopic,
            message.CorrelateWith(new ItemsDelivered { DeliveryRequest = message.Body.DeliveryRequest }));
    }
}