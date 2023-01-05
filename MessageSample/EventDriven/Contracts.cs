namespace MessageSample.EventDriven;

public class OrderPlaced
{
    public int Guest { get; set; }
    public int Order { get; set; }
    public int[] Food { get; set; }
    public int[] Drink { get; set; }
}

public class FoodCooked
{
    public int Order { get; set; }
    public int Food { get; set; }
}