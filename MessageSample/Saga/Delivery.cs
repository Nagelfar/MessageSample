using MessageSample.CommandDriven;

namespace MessageSample.Saga;

public class DeliveryHandler : IHandleMessage<DeliverItems>, IHandleMessage<DeliverCookedFood>
{
    private readonly ILogger<DeliveryHandler> _logger;

    public DeliveryHandler(ILogger<DeliveryHandler> logger)
    {
        _logger = logger;
    }

    public void Message(DeliverItems command)
    {
        _logger.LogInformation("CommandDrivenPipeline: Deliver ordered items for {@Message}", command);
    }

    public void Message(DeliverCookedFood message)
    {
        _logger.LogInformation("CommandDrivenPipeline: Deliver Cooked Food for {@Message}", message);
    }
}