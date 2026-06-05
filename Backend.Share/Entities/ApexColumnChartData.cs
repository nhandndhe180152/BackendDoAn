using System;

namespace Backend.Share.Entities;

public class ApexColumnChartData<T>
{
    public List<ApexChartData<T>> Series { get; set; } = new List<ApexChartData<T>>();
    public List<string> Categories { get; set; } = new List<string>();
    public List<string> Colors { get; set; } = new List<string>();
}
