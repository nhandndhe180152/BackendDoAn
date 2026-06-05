using System;

namespace Backend.Application.DTOs.ExportExcels;

public class ExportExcelRequest<T>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? TimePeriod { get; set; }
    public List<string>? RegionNames { get; set; }
    public string FileName { get; set; } = "Excel_File";
    public List<T> Data { get; set; } = new List<T>();
    public List<string> SelectedFields { get; set; } = new List<string>();
    public string TemplatePath { get; set; }
    public int RowIndex { get; set; } = 0;
    public List<string> ListSheetName { get; set; } = new List<string>();
    public object? Summary { get; set; }
    public Dictionary<string, decimal>? SummaryDynamic { get; set; }
    public Dictionary<string, string>? DynamicColumnNames { get; set; }
    public int? DynamicStartIndex { get; set; }
    public List<string>? Headers { get; set; }
    public bool WriteHeader { get; set; } = true;
}