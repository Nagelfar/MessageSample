using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;

namespace MessageSample.Saga;

public class OrderRequest
{
    public int Guest { get; set; }
    public int[] Food { get; set; }
    public int[] Drink { get; set; }
}

[ApiController]
[Route("/Saga/[controller]")]
public class TableServiceController : ControllerBase
{
    private readonly IModel _model;
    private static int Orders = 0;

    public TableServiceController(IConnection connection)
    {
        _model = connection.CreateModel();
    }

    [HttpPost("orders")]
    public object Post(OrderRequest? order)
    {
        if (order == null || order.Guest < 0 || order.Food.Any(food => food < 0) || order.Drink.Any(drink => drink < 0))
            return this.BadRequest("You provided an invalid model");
        var currentOrder = Interlocked.Increment(ref Orders);
        var orderPlaced =
            new OrderPlaced
            {
                Guest = order.Guest,
                Order = currentOrder,
                Food = order.Food,
                Drink = order.Drink
            };
        var correlationId = $"order-request-{currentOrder}";
        var envelope = Envelope.Create(orderPlaced, correlationId);
        _model.Publish(Topology.TableServiceTopic,envelope);
        return this.Ok();
    }
}