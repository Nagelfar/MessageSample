namespace MessageSample.CommandDrivenPipeline;

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


public class DeliverCookedFood
{
    public int Order { get; set; }
    public int Food { get; set; }
}