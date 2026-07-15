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

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ công nợ 2 chiều (Phải thu / Phải trả).
/// </summary>
public class PartyDebtService : IPartyDebtService
{
    private readonly IPartyDebtRepository _partyDebtRepository;
    private readonly IDebtTransactionRepository _debtTransactionRepository;

    public PartyDebtService(
        IPartyDebtRepository partyDebtRepository,
        IDebtTransactionRepository debtTransactionRepository)
    {
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
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

        var dtos = entities.Select(x => new PartyDebtDetailDto
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
            LastModifiedDate = x.LastModifiedDate
        }).ToList();

        return ApiResponse.Success(dtos);
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _partyDebtRepository.GetPagedAsync(parameters);
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

        return await ApplyTransactionAsync(dto, "CHARGE", isDebit: true);
    }

    public async Task<ApiResponse> PaymentAsync(CreateDebtTransactionDto dto)
    {
        if (dto.Amount <= 0)
            return ApiResponse.BadRequest(message: "Số tiền thanh toán phải lớn hơn 0.");

        return await ApplyTransactionAsync(dto, "PAYMENT", isDebit: false);
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
