using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;

namespace MessageSample.CommandDrivenPipeline;

public class OrderRequest
{
    public int Guest { get; set; }
    public int[] Food { get; set; }
    public int[] Drink { get; set; }
}

[ApiController]
[Route("/commanddrivenpipeline/[controller]")]
public class TableServiceController : ControllerBase
{
    private readonly IModel _model;
    private static int Orders = 0;

    public TableServiceController(IConnection connection)
    {
        _model = connection.CreateModel();
    }

    private void SendToCook(int orderId, int[] food)
    {
        var cookFoodCommands =
            food
                .Select(x => new CookFood { Food = x, Order = orderId })
                .ToArray();

        foreach (var command in cookFoodCommands)
        {
            _model.Send(Topology.FoodPreparationQueue, Envelope.Create(command));
        }
    }

    private void SendToDelivery(int orderId, int[] drink, int guest)
    {
        var command = new DeliverItems
        {
            Order = orderId,
            Drinks = drink,
            Guest = guest
        };
        _model.Send(Topology.DeliveryQueue, Envelope.Create(command));
    }

    [HttpPost("orders")]
    public object Post(OrderRequest? order)
    {
        if (order == null || order.Guest < 0 || order.Food.Any(food => food < 0) || order.Drink.Any(drink => drink < 0))
            return this.BadRequest("You provided an invalid model");
        var currentOrder = Interlocked.Increment(ref Orders);
        try
        {
            _model.TxSelect();
            SendToCook(currentOrder, order.Food);
            SendToDelivery(currentOrder, order.Drink, order.Guest);
            _model.TxCommit();
        }
        catch (Exception e)
        {
            _model.TxRollback();
        }

        return this.Ok();
    }
}