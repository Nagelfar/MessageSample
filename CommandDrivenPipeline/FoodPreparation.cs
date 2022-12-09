using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDrivenPipeline;

public class FoodPreparationHandler : IHandleMessage<CookFood>
{
    private readonly ILogger<FoodPreparationHandler> _logger;

    public FoodPreparationHandler(ILogger<FoodPreparationHandler> logger)
    {
        _logger = logger;
    }
    public void Message(CookFood command)
    {
        _logger.LogInformation("Cooking Food for {@Command}", command);
    }
}
