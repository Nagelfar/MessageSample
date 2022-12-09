namespace MessageSample.CommandDrivenPipeline;

public class DeliveryHandler : IHandleMessage<DeliverItems>
{
    private readonly ILogger<DeliveryHandler> _logger;

    public DeliveryHandler(ILogger<DeliveryHandler> logger)
    {
        _logger = logger;
    }

    public void Message(DeliverItems command)
    {
        _logger.LogInformation("Delivery items for {@Command}", command);
    }
}