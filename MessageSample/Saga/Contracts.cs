namespace MessageSample.Saga;

public interface IAmAnEvent
{
}

public interface IAmACommand
{
}

public class OrderPlaced : IAmAnEvent
{
    public int Guest { get; set; }
    public int Order { get; set; }
    public int[] Food { get; set; }
    public int[] Drink { get; set; }
}

public class CookFood : IAmACommand
{
    public int Order { get; set; }
    public int Food { get; set; }
}

public class FoodCooked : IAmAnEvent
{
    public int Order { get; set; }
    public int Food { get; set; }
}

public class DeliverDrinks : IAmACommand
{
    public Guid DeliveryRequest { get; set; }
    public int Order { get; set; }
    public int[] Drinks { get; set; }
    public int Guest { get; set; }
}

public class ItemsDelivered : IAmAnEvent
{
    public Guid DeliveryRequest { get; set; }
}

public class DeliverCookedFood : IAmACommand
{
    public Guid DeliveryRequest { get; set; }
    public int Guest { get; set; }
    public int Order { get; set; }
    public int Food { get; set; }
}