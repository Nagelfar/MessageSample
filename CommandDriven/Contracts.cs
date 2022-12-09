namespace MessageSample.CommandDriven;

public class CookFood
{
    public int Order { get; set; }
    public int Food { get; set; }
}

public class DeliverItems
{
    public int Order { get; set; }
    public int[] Drinks { get; set; }
    public int Guest { get; set; }
}