namespace MessageSample.DocumentDriven;

public class OrderDocument
{
    public int Guest { get; set; }
    public int Order { get; set; }

    public int[]? OrderedFood { get; set; }
    public int[] OrderedDrink { get; set; }
    public int[]? CookedFood { get; set; }
    public int[]? DeliveredFood { get; set; }

    public OrderDocument Clone()
    {
        return (OrderDocument)this.MemberwiseClone();
    }
}