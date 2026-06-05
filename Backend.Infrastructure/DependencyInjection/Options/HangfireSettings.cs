using System;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class HangfireSettings
{
    public string Route { get; set; }
    public string ServerName { get; set; }
    public Dashboard Dashboard { get; set; }
    public string ConnectionString { get; set; }
}
public class Dashboard
{
    public string AppPath { get; set; }
    public int StatsPollingInterval { get; set; } = 200;
    public string DashboardTitle { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
