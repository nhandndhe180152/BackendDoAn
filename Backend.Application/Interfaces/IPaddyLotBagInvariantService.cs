namespace Backend.Application.Interfaces;

public interface IPaddyLotBagInvariantService
{
    Task ValidateBagAsync(int bagId, CancellationToken cancellationToken = default);
    Task ValidateLotLocationAsync(int lotId, int locationId, CancellationToken cancellationToken = default);
}
