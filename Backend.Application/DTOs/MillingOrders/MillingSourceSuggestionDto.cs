namespace Backend.Application.DTOs.MillingOrders;

public class MillingSourceSuggestionDto
{
    public int PaddyLotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal SuggestedWeightKg { get; set; }
    public decimal ImmediatelyRetrievableKg { get; set; }
    public List<int> BagIds { get; set; } = new();
}

public class MillingSourceSuggestionResultDto
{
    public decimal RequiredWeightKg { get; set; }
    public decimal SuggestedWeightKg { get; set; }
    public decimal MissingWeightKg { get; set; }
    public bool IsComplete => MissingWeightKg <= 0.0005m;
    public List<MillingSourceSuggestionDto> Inputs { get; set; } = new();
    public List<MillingSourceSuggestionColumnDto> Columns { get; set; } = new();
}

public class MillingSourceSuggestionColumnDto
{
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal SuggestedWeightKg { get; set; }
    public List<int> BagIds { get; set; } = new();
}
