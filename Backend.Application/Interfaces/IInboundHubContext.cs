using System.Threading.Tasks;

namespace Backend.Application.Interfaces;

public interface IInboundHubContext
{
    Task PublishReceiptConfirmedAsync(int receiptId, int inboundOrderId);
}
