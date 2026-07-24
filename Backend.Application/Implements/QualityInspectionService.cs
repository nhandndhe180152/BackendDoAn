using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ kiểm tra chất lượng lô lúa/gạo (QualityInspection).
/// </summary>
public class QualityInspectionService : IQualityInspectionService
{
    private readonly IQualityInspectionRepository _repo;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly INotificationDispatcher _notificationDispatcher;

    public QualityInspectionService(
        IQualityInspectionRepository repo,
        IPaddyLotRepository paddyLotRepository,
        INotificationDispatcher notificationDispatcher)
    {
        _repo = repo;
        _paddyLotRepository = paddyLotRepository;
        _notificationDispatcher = notificationDispatcher;
    }

    public async Task<ApiResponse> CreateAsync(CreateQualityInspectionDto obj)
    {
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot == null || lot.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        var now = DateTimeHelper.VietnamNow();
        var entity = new QualityInspection
        {
            PaddyLotId = obj.PaddyLotId,
            InspectorId = obj.InspectorId,
            InspectedAt = obj.InspectedAt,
            MoisturePercent = obj.MoisturePercent,
            ImpurityPercent = obj.ImpurityPercent,
            MoldLevel = obj.MoldLevel?.Trim(),
            PestLevel = obj.PestLevel?.Trim(),
            PackagingStatus = obj.PackagingStatus?.Trim(),
            PassedInspection = obj.PassedInspection,
            Handling = obj.Handling?.Trim(),
            Note = obj.Note?.Trim(),
            CreatedBy = obj.CreatedBy,
            CreatedDate = now
        };

        // Bọc trong transaction: tạo phiếu kiểm tra và cập nhật QualityStatus cùng lúc (#12)
        await using var tx = await _repo.BeginTransactionAsync();
        try
        {
            await _repo.CreateAsync(entity);
            await _repo.SaveChangesAsync();

            // Cập nhật QualityStatus trên PaddyLot
            lot.QualityStatus = obj.PassedInspection ? "PASSED" : "FAILED";
            lot.LastModifiedDate = now;
            await _paddyLotRepository.UpdateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Giao phiếu cho người kiểm định -> thông báo + push FCM cho người được giao.
        // (Không tự gửi lại cho chính người tạo nếu tự nhận kiểm.)
        if (entity.InspectorId.HasValue && entity.InspectorId != obj.CreatedBy)
        {
            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.QualityInspectionAssigned,
                new NotificationTarget { UserIds = new List<int> { entity.InspectorId.Value } },
                new object[] { lot.LotCode },
                "/admin/quality-inspections",
                obj.CreatedBy);
        }

        return ApiResponse.Created(entity.Id, "Tạo phiếu kiểm tra chất lượng thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _repo
            .FindByCondition(x => !x.IsDeleted, false, x => x.PaddyLot)
            .OrderByDescending(x => x.InspectedAt)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _repo
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.PaddyLot)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDto(entity));
    }

    public async Task<ApiResponse> GetByLotAsync(int paddyLotId)
    {
        var entities = await _repo
            .FindByCondition(x => x.PaddyLotId == paddyLotId && !x.IsDeleted, false, x => x.PaddyLot)
            .OrderByDescending(x => x.InspectedAt)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _repo.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdateQualityInspectionDto obj)
    {
        var entity = await _repo.GetByIdAsync(obj.Id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        entity.PaddyLotId = obj.PaddyLotId;
        entity.InspectorId = obj.InspectorId;
        entity.InspectedAt = obj.InspectedAt;
        entity.MoisturePercent = obj.MoisturePercent;
        entity.ImpurityPercent = obj.ImpurityPercent;
        entity.MoldLevel = obj.MoldLevel?.Trim();
        entity.PestLevel = obj.PestLevel?.Trim();
        entity.PackagingStatus = obj.PackagingStatus?.Trim();
        entity.PassedInspection = obj.PassedInspection;
        entity.Handling = obj.Handling?.Trim();
        entity.Note = obj.Note?.Trim();
        entity.UpdatedBy = obj.UpdatedBy;
        entity.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _repo.UpdateAsync(entity);
        await _repo.SaveChangesAsync();

        // Đồng bộ QualityStatus về lô lúa/gạo (#14: giữ nhất quán khi phiếu bị cập nhật)
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot != null && !lot.IsDeleted)
        {
            lot.QualityStatus = obj.PassedInspection ? "PASSED" : "FAILED";
            lot.LastModifiedDate = entity.LastModifiedDate;
            await _paddyLotRepository.UpdateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();
        }

        // Kiểm định xong (ghi nhận kết quả) -> thông báo lại cho người TẠO phiếu.
        if (entity.CreatedBy.HasValue && entity.CreatedBy != obj.UpdatedBy)
        {
            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.QualityInspectionResult,
                new NotificationTarget { UserIds = new List<int> { entity.CreatedBy.Value } },
                new object[]
                {
                    lot?.LotCode ?? ("#" + entity.PaddyLotId),
                    obj.PassedInspection ? "Đạt" : "Cách ly (không đạt)"
                },
                "/admin/quality-inspections",
                obj.UpdatedBy);
        }

        return ApiResponse.Success(entity.Id, "Cập nhật phiếu kiểm tra thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _repo.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _repo.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    private static QualityInspectionDetailDto ToDto(QualityInspection x) => new()
    {
        Id = x.Id,
        PaddyLotId = x.PaddyLotId,
        LotCode = x.PaddyLot?.LotCode,
        InspectorId = x.InspectorId,
        InspectedAt = x.InspectedAt,
        MoisturePercent = x.MoisturePercent,
        ImpurityPercent = x.ImpurityPercent,
        MoldLevel = x.MoldLevel,
        PestLevel = x.PestLevel,
        PackagingStatus = x.PackagingStatus,
        PassedInspection = x.PassedInspection,
        Handling = x.Handling,
        Note = x.Note,
        CreatedDate = x.CreatedDate,
        LastModifiedDate = x.LastModifiedDate
    };
}
