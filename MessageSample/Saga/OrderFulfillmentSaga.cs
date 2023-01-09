using RabbitMQ.Client;

namespace MessageSample.Saga;

public record DeliveryRequest(Guid RequestId, bool Delivered);

public record FoodPreps(int Food, DeliveryRequest? DeliveryRequest);

public class OrderFulfillmentState
{
    public DeliveryRequest DrinkDeliveryRequest { get; set; }
    public FoodPreps[] FoodPreps { get; set; }
    public int Guest { get; set; }
}

public class DidFoodPreparationFinish
{
    public int Order { get; set; }
}

public class DidItemDeliverySucceed
{
    public Guid DeliveryRequest { get; set; }
}

public class OrderFulfillmentSaga :
    SagaBase<OrderFulfillmentState>,
    IHandleMessageEnvelope<OrderPlaced>,
    IHandleMessageEnvelope<FoodCooked>,
    IHandleMessageEnvelope<ItemsDelivered>,
    IHandleTimeout<DidFoodPreparationFinish>,
    IHandleTimeout<DidItemDeliverySucceed>
{
    public OrderFulfillmentSaga(IConnection connection) : base(connection)
    {
    }

    public void Message(Envelope<OrderPlaced> message)
    {
        var foodCommands =
            message.Body.Food
                .Select(x => new CookFood { Food = x, Order = message.Body.Order })
                .Select(message.CorrelateWith);
        Send(Topology.FoodPreparationQueue, foodCommands);
        var drinkCommand =
            new DeliverDrinks
            {
                Drinks = message.Body.Drink, Order = message.Body.Order, Guest = message.Body.Guest,
                DeliveryRequest = Guid.NewGuid()
            };
        Send(Topology.DeliveryQueue, message.CorrelateWith(drinkCommand));
        RequestTimeout(
            message.CorrelateWith(
                new DidFoodPreparationFinish
                {
                    Order = message.Body.Order
                }),
            TimeSpan.FromSeconds(10.0)
        );
        RequestTimeout(
            message.CorrelateWith(
                new DidItemDeliverySucceed
                {
                    DeliveryRequest = drinkCommand.DeliveryRequest
                }),
            TimeSpan.FromSeconds(10.0)
        );
        this.State(message.CorrelationId).Guest = message.Body.Guest;
        this.State(message.CorrelationId).DrinkDeliveryRequest =
            new DeliveryRequest(drinkCommand.DeliveryRequest, false);
        this.State(message.CorrelationId).FoodPreps =
            message.Body.Food.Select(foodId => new FoodPreps(foodId, null)).ToArray();
    }

    public void Message(Envelope<FoodCooked> message)
    {
        var foodCommand =
            new DeliverCookedFood()
            {
                Food = message.Body.Food,
                Order = message.Body.Order,
                Guest = this.State(message.CorrelationId).Guest,
                DeliveryRequest = Guid.NewGuid()
            };
        this.State(message.CorrelationId).FoodPreps =
            this.State(message.CorrelationId)
                .FoodPreps
                .Select(x =>
                {
                    return x.Food == message.Body.Food && x.DeliveryRequest == null
                        ? new FoodPreps(x.Food, new DeliveryRequest(foodCommand.DeliveryRequest, false))
                        : x;
                }).ToArray();
        this.Send(Topology.DeliveryQueue, message.CorrelateWith(foodCommand));
    }

    public void Message(Envelope<ItemsDelivered> message)
    {
        var state = this.State(message.CorrelationId);
        if (state.DrinkDeliveryRequest.RequestId == message.Body.DeliveryRequest)
            state.DrinkDeliveryRequest =
                state.DrinkDeliveryRequest with { Delivered = true };
        state.FoodPreps =
            state.FoodPreps
                .Select(x =>
                    x.DeliveryRequest?.RequestId == message.Body.DeliveryRequest
                        ? x with { DeliveryRequest = x.DeliveryRequest with { Delivered = true } }
                        : x)
                .ToArray();
    }

    public void Timeout(Envelope<DidFoodPreparationFinish> message)
    {
        var unprepared =
            this.State(message.CorrelationId).FoodPreps
                .Where(x => x.DeliveryRequest == null)
                .ToList();
        // if any food is not prepared ask the cook to retry
        if (unprepared.Any())
        {
            Send(Topology.FoodPreparationQueue,
                unprepared
                    .Select(x => new CookFood { Food = x.Food, Order = message.Body.Order })
                    .Select(message.CorrelateWith)
            );
        }
    }

    public void Timeout(Envelope<DidItemDeliverySucceed> message)
    {
        // TODO should we ask to deliver again?
    }
}