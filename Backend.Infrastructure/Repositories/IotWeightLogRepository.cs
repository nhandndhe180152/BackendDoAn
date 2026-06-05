using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class IotWeightLogRepository : RepositoryBase<IotWeightLog, int>, IIotWeightLogRepository
{
    private readonly BackendContext _context;

    public IotWeightLogRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    /// <summary>
    /// Thực hiện truy vấn hoặc cập nhật dữ liệu ở tầng Infrastructure bằng EF Core.
    /// </summary>
    /// <param name="iotDeviceId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotWeightLog?> GetLatestByDeviceIdAsync(int iotDeviceId)
    {
        return await _context.IotWeightLogs
            .Where(x => !x.IsDeleted && x.IoTDeviceId == iotDeviceId)
            .OrderByDescending(x => x.MeasuredAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Lấy chi tiết một bản ghi theo id. Nếu không tìm thấy thì tầng service sẽ trả NotFound để API phản hồi 404.
    /// </summary>
    /// <param name="id">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotWeightLog?> GetByIdForAttachAsync(int id)
    {
        return await _context.IotWeightLogs
            .Include(x => x.IotDevice)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}
