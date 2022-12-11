using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;

namespace MessageSample.DocumentDriven;

public class OrderRequest
{
    public int Guest { get; set; }
    public int[] Food { get; set; }
    public int[] Drink { get; set; }
}

[ApiController]
[Route("/documentdriven/[controller]")]
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
            new OrderDocument
            {
                Guest = order.Guest,
                Order = currentOrder,
                OrderedFood = order.Food,
                OrderedDrink = order.Drink
            };
        _model.BasicPublish(exchange: Topology.OrdersTopic, "", body: orderPlaced.Serialize());
        return this.Ok();
    }
}