using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.QrCode;

namespace Backend.Application.Interfaces;

public interface IQrIdentifierService
{
    Task<EnsureQrResponseDto> EnsurePaddyLotQrCodeAsync(int paddyLotId, CancellationToken cancellationToken);
    Task<EnsureQrResponseDto> EnsureLocationQrCodeAsync(int locationId, CancellationToken cancellationToken);
    Task<EnsureQrResponseDto> RegeneratePaddyLotQrCodeAsync(int paddyLotId, string reason, CancellationToken cancellationToken);
    Task<EnsureQrResponseDto> RegenerateLocationQrCodeAsync(int locationId, string reason, CancellationToken cancellationToken);
}
