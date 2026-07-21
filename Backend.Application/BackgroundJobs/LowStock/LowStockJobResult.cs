namespace Backend.Application.BackgroundJobs.LowStock;

public class LowStockJobResult
{
    public int Processed { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Resolved { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long DurationMs { get; set; }
}
