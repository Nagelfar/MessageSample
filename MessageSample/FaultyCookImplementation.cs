namespace MessageSample;

public class FaultyCookImplementation
{
    public double FaultThreshold { get; }
    private readonly Random _randomizer;

    private FaultyCookImplementation(double faultThreshold)
    {
        if(faultThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(faultThreshold), "The fault threshold must be between 0 and 1.");
        FaultThreshold = faultThreshold;
        _randomizer = new Random();
    }
    
    public static FaultyCookImplementation Create(double faultyCookThreshold)
    {
        return new FaultyCookImplementation(Math.Min(Math.Max(0.0, faultyCookThreshold), 1.0));
    }

    public void Operate()
    {
        Thread.Sleep(2000);
        if (_randomizer.NextDouble() < FaultThreshold)
            throw new ApplicationException("Cook is failing");
    }
}