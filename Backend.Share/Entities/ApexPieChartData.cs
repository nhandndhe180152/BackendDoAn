using System;

namespace Backend.Share.Entities;

public class ApexPieChartData<T>
{
    public List<T> Series { get; set; } = new List<T>();
    public List<string> Labels { get; set; } = new List<string>();
    public List<string> Colors { get; set; } = new List<string>();
}
