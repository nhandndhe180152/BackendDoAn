using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.PartyDebts;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Backend.Application.Constants;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Share.Services;
using System.Collections.Generic;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ công nợ 2 chiều (Phải thu / Phải trả).
/// </summary>
public class PartyDebtService : IPartyDebtService
{
    private readonly IPartyDebtRepository _partyDebtRepository;
    private readonly IDebtTransactionRepository _debtTransactionRepository;
    private readonly IDebtAgingCalculationService _agingService;
    private readonly IScheduledJobService _scheduledJobService;

    public PartyDebtService(
        IPartyDebtRepository partyDebtRepository,
        IDebtTransactionRepository debtTransactionRepository,
        IDebtAgingCalculationService agingService,
        IScheduledJobService scheduledJobService)
    {
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _agingService = agingService;
        _scheduledJobService = scheduledJobService;
    }

    public async Task<ApiResponse> GetByPartyAsync(string partyType, int partyId, int? organizationId)
    {
        var query = _partyDebtRepository
            .FindByCondition(x =>
                !x.IsDeleted &&
                x.PartyType == partyType.ToUpper() &&
                x.PartyId == partyId);

        if (organizationId.HasValue)
            query = query.Where(x => x.OrganizationId == organizationId.Value);

        var entities = await query.ToListAsync();
        var entityIds = entities.Select(e => e.Id).ToList();
        var now = DateTimeHelper.VietnamNow();
        var agingMap = await _agingService.CalculatePartyDebtsAgingBatchAsync(entityIds, now, default);

        var dtos = entities.Select(x => {
            agingMap.TryGetValue(x.Id, out var aging);
            return new PartyDebtDetailDto
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                PartyType = x.PartyType,
                PartyId = x.PartyId,
                Direction = x.Direction,
                OpeningBalance = x.OpeningBalance,
                CurrentBalance = x.CurrentBalance,
                CreditLimit = x.CreditLimit,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
                LastModifiedDate = x.LastModifiedDate,
                DueSoonAmount = aging?.DueSoonAmount ?? 0m,
                DueTodayAmount = aging?.DueTodayAmount ?? 0m,
                OverdueAmount = aging?.OverdueAmount ?? 0m,
                OldestOverdueDate = aging?.OldestOverdueDate,
                MaxDaysOverdue = aging?.MaxDaysOverdue ?? 0
            };
        }).ToList();

        return ApiResponse.Success(dtos);
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _partyDebtRepository.GetPagedAsync(parameters);
        if (data != null && data.data != null && data.data.Any())
        {
            var entityIds = data.data.Select(d => d.Id).ToList();
            var now = DateTimeHelper.VietnamNow();
            var agingMap = await _agingService.CalculatePartyDebtsAgingBatchAsync(entityIds, now, default);

            foreach (var item in data.data)
            {
                if (agingMap.TryGetValue(item.Id, out var aging))
                {
                    item.DueSoonAmount = aging.DueSoonAmount;
                    item.DueTodayAmount = aging.DueTodayAmount;
                    item.OverdueAmount = aging.OverdueAmount;
                    item.OldestOverdueDate = aging.OldestOverdueDate;
                    item.MaxDaysOverdue = aging.MaxDaysOverdue;
                }
            }
        }
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetTransactionsAsync(int partyDebtId, DTParameter parameters)
    {
        var debt = await _partyDebtRepository.GetByIdAsync(partyDebtId);
        if (debt == null || debt.IsDeleted)
            return ApiResponse.NotFound();

        var data = await _debtTransactionRepository.GetPagedByPartyDebtAsync(partyDebtId, parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> ChargeAsync(CreateDebtTransactionDto dto)
    {
        if (dto.Amount <= 0)
            return ApiResponse.BadRequest(message: "Số tiền phát sinh nợ phải lớn hơn 0.");

        return await ApplyTransactionAsync(dto, LookupCodes.DebtTransactionType.Charge, isDebit: true);
    }

    public async Task<ApiResponse> PaymentAsync(CreateDebtTransactionDto dto)
    {
        if (dto.Amount <= 0)
            return ApiResponse.BadRequest(message: "Số tiền thanh toán phải lớn hơn 0.");

        return await ApplyTransactionAsync(dto, LookupCodes.DebtTransactionType.Payment, isDebit: false);
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private async Task<ApiResponse> ApplyTransactionAsync(
        CreateDebtTransactionDto dto,
        string transactionType,
        bool isDebit)
    {
        var debt = await _partyDebtRepository.GetByIdAsync(dto.PartyDebtId);
        if (debt == null || debt.IsDeleted)
            return ApiResponse.NotFound(message: "Sổ công nợ không tìm thấy.");

        if (!debt.IsActive)
            return ApiResponse.UnprocessableEntity("Sổ công nợ đang bị khoá.");

        var now = DateTimeHelper.VietnamNow();
        var balanceBefore = debt.CurrentBalance;

        // Phát sinh nợ: cộng số dư. Thanh toán: trừ số dư.
        // #11: Ngăn số dư âm — thanh toán không được vượt quá dư nợ hiện tại
        if (!isDebit && dto.Amount > balanceBefore)
            return ApiResponse.UnprocessableEntity(
                $"Số tiền thanh toán ({dto.Amount:N0} VNĐ) vượt quá dư nợ hiện tại ({balanceBefore:N0} VNĐ). " +
                "Vui lòng kiểm tra lại.");

        debt.CurrentBalance = isDebit
            ? balanceBefore + dto.Amount
            : balanceBefore - dto.Amount;

        debt.LastModifiedDate = now;
        await _partyDebtRepository.UpdateAsync(debt);

        var tx = new DebtTransaction
        {
            PartyDebtId = dto.PartyDebtId,
            TransactionType = transactionType,
            Amount = dto.Amount,
            BalanceAfter = debt.CurrentBalance,
            RefType = dto.RefType?.Trim().ToUpper(),
            RefId = dto.RefId,
            TransactionDate = dto.TransactionDate,
            DueDate = dto.DueDate,
            Note = dto.Note?.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = now
        };

        await _debtTransactionRepository.CreateAsync(tx);
        await _debtTransactionRepository.SaveChangesAsync();

        // Enqueue targeted background job evaluation for JOB-04 after transaction completes
        _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(debt.Id, CancellationToken.None));

        var responseDto = new DebtTransactionDetailDto
        {
            Id = tx.Id,
            PartyDebtId = tx.PartyDebtId,
            TransactionType = tx.TransactionType,
            Amount = tx.Amount,
            BalanceAfter = tx.BalanceAfter,
            RefType = tx.RefType,
            RefId = tx.RefId,
            TransactionDate = tx.TransactionDate,
            DueDate = tx.DueDate,
            Note = tx.Note,
            CreatedDate = tx.CreatedDate
        };

        return ApiResponse.Success(responseDto,
            isDebit ? "Ghi phát sinh nợ thành công." : "Ghi thanh toán thành công.");
    }
}
