using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.API.Hubs;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Backend.API.Utilities;

public class DataChangeNotifier : IDataChangeNotifier
{
    private readonly IHubContext<DataChangeHub> _hubContext;

    public DataChangeNotifier(IHubContext<DataChangeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyEntitiesChangedAsync(IReadOnlyCollection<string> entityNames)
    {
        return _hubContext.Clients.All.SendAsync("EntityChanged", entityNames);
    }
}
