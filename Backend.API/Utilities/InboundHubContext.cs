using System.Threading.Tasks;
using Backend.API.Hubs;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Backend.API.Utilities;

public class InboundHubContext : IInboundHubContext
{
    private readonly IHubContext<InboundHub> _hubContext;

    public InboundHubContext(IHubContext<InboundHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishReceiptConfirmedAsync(int receiptId, int inboundOrderId)
    {
        await _hubContext.Clients.All.SendAsync("InboundReceiptConfirmed", new { ReceiptId = receiptId, InboundOrderId = inboundOrderId });
    }
}
