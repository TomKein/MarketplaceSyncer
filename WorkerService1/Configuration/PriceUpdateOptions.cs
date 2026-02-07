namespace WorkerService1.Configuration;

public class PriceUpdateOptions
{
    public int BatchSize { get; set; } = 250;
    
    public int BatchFilterSize { get; set; } = 50;
    
    public string TargetPriceTypeId { get; set; } = "75524";
    
    public decimal IncreasePercentage { get; set; } = 15.0m;
    
    public int RoundToNearest { get; set; } = 50;
}
