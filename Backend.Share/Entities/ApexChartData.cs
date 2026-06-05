using System;

namespace Backend.Share.Entities;

public class ApexChartData<T>
{
    public string Name { get; set; } = null!;
    public List<T> Data { get; set; }=new List<T>();
}
