using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.PartyDebts;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;
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
    private readonly IApplicationDbContext? _context;

    public PartyDebtService(
        IPartyDebtRepository partyDebtRepository,
        IDebtTransactionRepository debtTransactionRepository,
        IDebtAgingCalculationService agingService,
        IScheduledJobService scheduledJobService,
        IApplicationDbContext? context = null)
    {
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _agingService = agingService;
        _scheduledJobService = scheduledJobService;
        _context = context;
    }

    public async Task<ApiResponse> GetByPartyAsync(string partyType, int partyId, int? organizationId)
    {
        if (_context == null)
            return ApiResponse.Error(message: "Không thể truy cập dữ liệu công nợ.", status: 500);

        var normalizedPartyType = (partyType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedPartyType != LookupCodes.PartyType.Farmer &&
            normalizedPartyType != LookupCodes.PartyType.Customer)
        {
            return ApiResponse.BadRequest(message: "Loại đối tác không hợp lệ.");
        }

        var query = BuildDebtQuery()
            .Where(x => x.PartyType == normalizedPartyType && x.PartyId == partyId);

        if (organizationId.HasValue)
            query = query.Where(x => x.OrganizationId == organizationId.Value);

        var entities = await query.AsNoTracking().ToListAsync();
        var dtos = await MapDebtRowsAsync(entities);
        return ApiResponse.Success(dtos);
    }

    public async Task<ApiResponse> GetPagedAsync(PartyDebtDTParameters parameters)
    {
        if (_context == null)
        {
            var legacyData = await _partyDebtRepository.GetPagedAsync(parameters);
            return ApiResponse.Success(legacyData);
        }

        parameters ??= new PartyDebtDTParameters();
        var query = BuildDebtQuery();
        var direction = (parameters.Direction ?? string.Empty).Trim().ToUpperInvariant();
        if (direction is LookupCodes.DebtDirection.Payable or LookupCodes.DebtDirection.Receivable)
            query = query.Where(x => x.Direction == direction);

        var entities = await query.AsNoTracking().ToListAsync();
        var recordsTotal = entities.Count;
        var rows = await MapDebtRowsAsync(entities);

        var keyword = parameters.Search?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            rows = rows.Where(x =>
                    x.PartyName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.PartyCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.PartyType.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.Direction.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (parameters.OverdueOnly)
            rows = rows.Where(x => x.OverdueAmount > 0m).ToList();

        var recordsFiltered = rows.Count;
        rows = ApplySort(rows, parameters).ToList();

        var start = Math.Max(0, parameters.Start);
        var length = parameters.Length <= 0 ? 10 : Math.Min(parameters.Length, 100);
        var pageRows = rows.Skip(start).Take(length).ToList();

        return ApiResponse.Success(new DTResult<PartyDebtDetailDto>
        {
            draw = parameters.Draw,
            recordsTotal = recordsTotal,
            recordsFiltered = recordsFiltered,
            data = pageRows
        });
    }

    public async Task<ApiResponse> GetSummaryAsync()
    {
        if (_context == null)
            return ApiResponse.Error(message: "Không thể truy cập dữ liệu công nợ.", status: 500);

        var entities = await BuildDebtQuery().AsNoTracking().ToListAsync();
        var rows = await MapDebtRowsAsync(entities);
        var documents = await BuildDocumentRowsAsync(entities);

        var summary = new PartyDebtSummaryDto
        {
            TotalPayable = rows
                .Where(x => x.Direction == LookupCodes.DebtDirection.Payable)
                .Sum(x => x.CurrentBalance),
            TotalReceivable = rows
                .Where(x => x.Direction == LookupCodes.DebtDirection.Receivable)
                .Sum(x => x.CurrentBalance),
            TotalOverduePayable = rows
                .Where(x => x.Direction == LookupCodes.DebtDirection.Payable)
                .Sum(x => x.OverdueAmount),
            TotalOverdueReceivable = rows
                .Where(x => x.Direction == LookupCodes.DebtDirection.Receivable)
                .Sum(x => x.OverdueAmount),
            OverLimitCustomerCount = rows.Count(x =>
                x.Direction == LookupCodes.DebtDirection.Receivable &&
                x.CreditLimit.HasValue &&
                x.CurrentBalance > x.CreditLimit.Value),
            ActiveDebtCount = rows.Count(x => x.IsActive && x.CurrentBalance > 0m),
            OpenDocumentCount = documents.Count(x => x.OutstandingAmount > 0m),
            OverdueDocumentCount = documents.Count(x => x.Status == "OVERDUE"),
            NetProjectedCashFlow = rows
                .Where(x => x.Direction == LookupCodes.DebtDirection.Receivable)
                .Sum(x => x.CurrentBalance) -
                rows.Where(x => x.Direction == LookupCodes.DebtDirection.Payable)
                    .Sum(x => x.CurrentBalance)
        };

        return ApiResponse.Success(summary);
    }

    public async Task<ApiResponse> GetDocumentsAsync(DebtDocumentDTParameters parameters)
    {
        if (_context == null)
            return ApiResponse.Error(message: "Không thể truy cập dữ liệu công nợ.", status: 500);

        parameters ??= new DebtDocumentDTParameters();
        var query = BuildDebtQuery();
        var direction = NormalizeDirection(parameters.Direction);
        if (direction != null)
            query = query.Where(x => x.Direction == direction);

        var debts = await query.AsNoTracking().ToListAsync();
        var rows = await BuildDocumentRowsAsync(debts);
        var recordsTotal = rows.Count;

        var keyword = parameters.Search?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            rows = rows.Where(x =>
                    ContainsIgnoreCase(x.PartyCode, keyword) ||
                    ContainsIgnoreCase(x.PartyName, keyword) ||
                    ContainsIgnoreCase(x.PartyPhone, keyword) ||
                    ContainsIgnoreCase(x.DocumentCode, keyword) ||
                    ContainsIgnoreCase(x.Note, keyword))
                .ToList();
        }

        if (parameters.OverdueOnly)
            rows = rows.Where(x => x.Status == "OVERDUE").ToList();

        var status = (parameters.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(status))
            rows = rows.Where(x => x.Status == status).ToList();

        var recordsFiltered = rows.Count;
        rows = ApplyDocumentSort(rows, parameters).ToList();
        var start = Math.Max(0, parameters.Start);
        var length = parameters.Length <= 0 ? 10 : Math.Min(parameters.Length, 100);

        return ApiResponse.Success(new DTResult<DebtDocumentDto>
        {
            draw = parameters.Draw,
            recordsTotal = recordsTotal,
            recordsFiltered = recordsFiltered,
            data = rows.Skip(start).Take(length).ToList()
        });
    }

    public async Task<ApiResponse> GetAllTransactionsAsync(DebtTransactionDTParameters parameters)
    {
        if (_context == null)
            return ApiResponse.Error(message: "Không thể truy cập dữ liệu công nợ.", status: 500);

        parameters ??= new DebtTransactionDTParameters();
        var debtQuery = BuildDebtQuery();
        var direction = NormalizeDirection(parameters.Direction);
        if (direction != null)
            debtQuery = debtQuery.Where(x => x.Direction == direction);

        var debts = await debtQuery.AsNoTracking().ToListAsync();
        var debtIds = debts.Select(x => x.Id).ToList();
        var txQuery = _context.DebtTransactions
            .AsNoTracking()
            .Where(x => debtIds.Contains(x.PartyDebtId) && !x.IsDeleted);

        var transactionType = (parameters.TransactionType ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(transactionType))
            txQuery = txQuery.Where(x => x.TransactionType == transactionType);
        if (parameters.DateFrom.HasValue)
            txQuery = txQuery.Where(x => x.TransactionDate >= parameters.DateFrom.Value.Date);
        if (parameters.DateTo.HasValue)
        {
            var dateToExclusive = parameters.DateTo.Value.Date.AddDays(1);
            txQuery = txQuery.Where(x => x.TransactionDate < dateToExclusive);
        }

        var transactions = await txQuery.ToListAsync();
        var partyMap = await LoadPartyInfoAsync(debts);
        var documentLabels = await ResolveDocumentLabelsAsync(
            transactions.Select(x => (x.RefType, x.RefId)).ToList());
        var userIds = transactions
            .Where(x => x.CreatedBy.HasValue)
            .Select(x => x.CreatedBy!.Value)
            .Distinct()
            .ToList();
        var users = await _context.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Username })
            .ToDictionaryAsync(x => x.Id);
        var debtMap = debts.ToDictionary(x => x.Id);

        var rows = transactions.Select(tx =>
        {
            debtMap.TryGetValue(tx.PartyDebtId, out var debt);
            partyMap.TryGetValue(tx.PartyDebtId, out var party);
            var refKey = BuildRefKey(tx.RefType, tx.RefId);
            documentLabels.TryGetValue(refKey, out var document);
            string? createdByName = null;
            if (tx.CreatedBy.HasValue && users.TryGetValue(tx.CreatedBy.Value, out var user))
            {
                createdByName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(createdByName)) createdByName = user.Username;
            }

            return new DebtTransactionListItemDto
            {
                Id = tx.Id,
                PartyDebtId = tx.PartyDebtId,
                PartyType = debt?.PartyType ?? string.Empty,
                PartyId = debt?.PartyId ?? 0,
                PartyCode = party?.Code ?? string.Empty,
                PartyName = party?.Name ?? $"Đối tác #{debt?.PartyId}",
                Direction = debt?.Direction ?? string.Empty,
                TransactionType = tx.TransactionType,
                Amount = tx.Amount,
                BalanceAfter = tx.BalanceAfter,
                RefType = tx.RefType,
                RefId = tx.RefId,
                DocumentCode = document?.Code ?? BuildFallbackDocumentCode(tx.RefType, tx.RefId),
                TransactionDate = tx.TransactionDate,
                DueDate = tx.DueDate,
                Note = tx.Note,
                CreatedDate = tx.CreatedDate,
                CreatedByName = createdByName
            };
        }).ToList();

        var keyword = parameters.Search?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            rows = rows.Where(x =>
                    ContainsIgnoreCase(x.PartyCode, keyword) ||
                    ContainsIgnoreCase(x.PartyName, keyword) ||
                    ContainsIgnoreCase(x.DocumentCode, keyword) ||
                    ContainsIgnoreCase(x.Note, keyword) ||
                    ContainsIgnoreCase(x.CreatedByName, keyword))
                .ToList();
        }

        var recordsFiltered = rows.Count;
        rows = ApplyTransactionSort(rows, parameters).ToList();
        var start = Math.Max(0, parameters.Start);
        var length = parameters.Length <= 0 ? 10 : Math.Min(parameters.Length, 100);

        return ApiResponse.Success(new DTResult<DebtTransactionListItemDto>
        {
            draw = parameters.Draw,
            recordsTotal = transactions.Count,
            recordsFiltered = recordsFiltered,
            data = rows.Skip(start).Take(length).ToList()
        });
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

        if (dto.TransactionDate == default)
            return ApiResponse.BadRequest(message: "Ngày thanh toán là bắt buộc.");

        if (!string.IsNullOrWhiteSpace(dto.Note) && dto.Note.Trim().Length > 500)
            return ApiResponse.BadRequest(message: "Ghi chú thanh toán không được vượt quá 500 ký tự.");

        return await ApplyTransactionAsync(dto, LookupCodes.DebtTransactionType.Payment, isDebit: false);
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private async Task<ApiResponse> ApplyTransactionAsync(
        CreateDebtTransactionDto dto,
        string transactionType,
        bool isDebit)
    {
        // Giữ đường chạy cũ cho unit test hoặc tác vụ nội bộ không có DbContext.
        if (_context == null)
            return await ApplyTransactionWithRepositoriesAsync(dto, transactionType, isDebit);

        var requestId = string.IsNullOrWhiteSpace(dto.RequestId)
            ? null
            : dto.RequestId.Trim();
        if (requestId?.Length > 100)
            return ApiResponse.BadRequest(message: "RequestId không được vượt quá 100 ký tự.");

        var deduplicationKey = requestId == null
            ? null
            : $"DEBT-{transactionType}-{dto.PartyDebtId}-{requestId}";

        if (deduplicationKey != null)
        {
            var existing = await _context.DebtTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeduplicationKey == deduplicationKey && !x.IsDeleted);
            if (existing != null)
                return ApiResponse.Success(ToTransactionDto(existing), "Giao dịch này đã được ghi nhận trước đó.");
        }

        await using var dbTransaction = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        DebtTransaction? tx = null;
        PartyDebt? debt = null;

        try
        {
            debt = await _context.PartyDebts
                .FirstOrDefaultAsync(x => x.Id == dto.PartyDebtId && !x.IsDeleted);
            if (debt == null)
                return ApiResponse.NotFound(message: "Sổ công nợ không tìm thấy.");

            if (!debt.IsActive)
                return ApiResponse.UnprocessableEntity("Sổ công nợ đang bị khoá.");

            var now = DateTimeHelper.VietnamNow();
            var balanceBefore = debt.CurrentBalance;

            if (!isDebit && dto.Amount > balanceBefore)
            {
                return ApiResponse.UnprocessableEntity(
                    $"Số tiền thanh toán ({dto.Amount:N0} VNĐ) vượt quá dư nợ hiện tại ({balanceBefore:N0} VNĐ). " +
                    "Vui lòng kiểm tra lại.");
            }

            if (!isDebit && !string.IsNullOrWhiteSpace(dto.RefType) && dto.RefId.HasValue)
            {
                var debtTransactions = await _context.DebtTransactions
                    .AsNoTracking()
                    .Where(x => x.PartyDebtId == debt.Id && !x.IsDeleted)
                    .ToListAsync();
                var targetDocument = _agingService
                    .CalculateDebtDocuments(debt, debtTransactions)
                    .Where(x => NormalizeRefType(x.RefType) == NormalizeRefType(dto.RefType) &&
                                x.RefId == dto.RefId)
                    .OrderBy(x => x.TransactionDate)
                    .FirstOrDefault(x => x.OutstandingAmount > 0m);

                if (targetDocument == null)
                    return ApiResponse.UnprocessableEntity("Chứng từ không còn dư nợ hoặc không thuộc sổ công nợ này.");

                if (dto.Amount > targetDocument.OutstandingAmount)
                {
                    return ApiResponse.UnprocessableEntity(
                        $"Số tiền thanh toán ({dto.Amount:N0} VNĐ) vượt quá số còn phải thanh toán của chứng từ " +
                        $"({targetDocument.OutstandingAmount:N0} VNĐ).");
                }
            }

            debt.CurrentBalance = isDebit
                ? balanceBefore + dto.Amount
                : balanceBefore - dto.Amount;
            debt.UpdatedBy = dto.CreatedBy;
            debt.LastModifiedDate = now;

            tx = new DebtTransaction
            {
                PartyDebtId = dto.PartyDebtId,
                TransactionType = transactionType,
                Amount = dto.Amount,
                BalanceAfter = debt.CurrentBalance,
                RefType = string.IsNullOrWhiteSpace(dto.RefType)
                    ? (isDebit ? "MANUAL_CHARGE" : "MANUAL_PAYMENT")
                    : dto.RefType.Trim().ToUpperInvariant(),
                RefId = dto.RefId,
                TransactionDate = dto.TransactionDate,
                DueDate = dto.DueDate,
                Note = dto.Note?.Trim(),
                DeduplicationKey = deduplicationKey,
                CreatedBy = dto.CreatedBy,
                CreatedDate = now
            };

            await _context.DebtTransactions.AddAsync(tx);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync();
            if (deduplicationKey != null)
            {
                var existing = await _context.DebtTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.DeduplicationKey == deduplicationKey && !x.IsDeleted);
                if (existing != null)
                    return ApiResponse.Success(ToTransactionDto(existing), "Giao dịch này đã được ghi nhận trước đó.");
            }
            throw;
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        // Cảnh báo là best-effort: lỗi Hangfire không được làm API báo thất bại
        // sau khi giao dịch công nợ đã commit thành công.
        try
        {
            _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(
                service => service.EvaluatePartyDebtAsync(debt!.Id, CancellationToken.None));
        }
        catch
        {
            // JOB-04 định kỳ lúc 07:00 vẫn sẽ đối soát lại.
        }

        return ApiResponse.Success(
            ToTransactionDto(tx!),
            isDebit ? "Ghi phát sinh nợ thành công." : "Đã lưu thanh toán.");
    }

    private IQueryable<PartyDebt> BuildDebtQuery()
    {
        return _context!.PartyDebts.Where(x => !x.IsDeleted);
    }

    private async Task<List<PartyDebtDetailDto>> MapDebtRowsAsync(List<PartyDebt> entities)
    {
        if (_context == null || entities.Count == 0)
            return new List<PartyDebtDetailDto>();

        var farmerIds = entities
            .Where(x => x.PartyType == LookupCodes.PartyType.Farmer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();
        var customerIds = entities
            .Where(x => x.PartyType == LookupCodes.PartyType.Customer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();

        var farmers = await _context.Farmers
            .AsNoTracking()
            .Where(x => farmerIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToDictionaryAsync(x => x.Id);
        var customers = await _context.Customers
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToDictionaryAsync(x => x.Id);

        var entityIds = entities.Select(x => x.Id).ToList();
        var agingMap = await _agingService.CalculatePartyDebtsAgingBatchAsync(
            entityIds,
            DateTimeHelper.VietnamNow(),
            CancellationToken.None);

        return entities.Select(x =>
        {
            agingMap.TryGetValue(x.Id, out var aging);
            var partyCode = string.Empty;
            var partyName = string.Empty;

            if (x.PartyType == LookupCodes.PartyType.Farmer &&
                farmers.TryGetValue(x.PartyId, out var farmer))
            {
                partyCode = farmer.Code;
                partyName = farmer.Name;
            }
            else if (x.PartyType == LookupCodes.PartyType.Customer &&
                     customers.TryGetValue(x.PartyId, out var customer))
            {
                partyCode = customer.Code;
                partyName = customer.Name;
            }

            return new PartyDebtDetailDto
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                PartyType = x.PartyType,
                PartyId = x.PartyId,
                PartyCode = partyCode,
                PartyName = string.IsNullOrWhiteSpace(partyName)
                    ? $"{x.PartyType} #{x.PartyId}"
                    : partyName,
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
    }

    private async Task<List<DebtDocumentDto>> BuildDocumentRowsAsync(List<PartyDebt> debts)
    {
        if (_context == null || debts.Count == 0)
            return new List<DebtDocumentDto>();

        var debtIds = debts.Select(x => x.Id).ToList();
        var transactions = await _context.DebtTransactions
            .AsNoTracking()
            .Where(x => debtIds.Contains(x.PartyDebtId) && !x.IsDeleted)
            .ToListAsync();
        var transactionMap = transactions
            .GroupBy(x => x.PartyDebtId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var partyMap = await LoadPartyInfoAsync(debts);

        var allocations = new List<(PartyDebt Debt, DebtDocumentAllocation Allocation)>();
        foreach (var debt in debts)
        {
            transactionMap.TryGetValue(debt.Id, out var debtTransactions);
            debtTransactions ??= new List<DebtTransaction>();
            allocations.AddRange(_agingService
                .CalculateDebtDocuments(debt, debtTransactions)
                .Select(x => (debt, x)));
        }

        var labels = await ResolveDocumentLabelsAsync(
            allocations.Select(x => (x.Allocation.RefType, x.Allocation.RefId)).ToList());
        var today = DateTimeHelper.VietnamNow().Date;

        return allocations.Select(item =>
        {
            partyMap.TryGetValue(item.Debt.Id, out var party);
            labels.TryGetValue(
                BuildRefKey(item.Allocation.RefType, item.Allocation.RefId),
                out var label);
            var dueDate = item.Allocation.DueDate?.Date;
            var status = item.Allocation.OutstandingAmount <= 0m
                ? "PAID"
                : dueDate.HasValue && dueDate.Value < today
                    ? "OVERDUE"
                    : item.Allocation.PaidAmount > 0m
                        ? "PARTIAL"
                        : "UNPAID";

            return new DebtDocumentDto
            {
                PartyDebtId = item.Debt.Id,
                ChargeTransactionId = item.Allocation.ChargeTransactionId,
                PartyType = item.Debt.PartyType,
                PartyId = item.Debt.PartyId,
                PartyCode = party?.Code ?? string.Empty,
                PartyName = party?.Name ?? $"{item.Debt.PartyType} #{item.Debt.PartyId}",
                PartyPhone = party?.Phone,
                Direction = item.Debt.Direction,
                RefType = item.Allocation.RefType,
                RefId = item.Allocation.RefId,
                DocumentCode = label?.Code ??
                               BuildFallbackDocumentCode(item.Allocation.RefType, item.Allocation.RefId),
                DocumentUrl = label?.Url,
                TransactionDate = item.Allocation.TransactionDate,
                DueDate = item.Allocation.DueDate,
                Note = item.Allocation.Note,
                TotalAmount = item.Allocation.TotalAmount,
                PaidAmount = item.Allocation.PaidAmount,
                OutstandingAmount = item.Allocation.OutstandingAmount,
                Status = status,
                DaysOverdue = dueDate.HasValue && dueDate.Value < today
                    ? (today - dueDate.Value).Days
                    : 0
            };
        }).ToList();
    }

    private async Task<Dictionary<int, PartyInfo>> LoadPartyInfoAsync(List<PartyDebt> debts)
    {
        var result = new Dictionary<int, PartyInfo>();
        if (_context == null || debts.Count == 0) return result;

        var farmerIds = debts
            .Where(x => x.PartyType == LookupCodes.PartyType.Farmer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();
        var customerIds = debts
            .Where(x => x.PartyType == LookupCodes.PartyType.Customer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();

        var farmers = await _context.Farmers
            .AsNoTracking()
            .Where(x => farmerIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name, x.Phone })
            .ToDictionaryAsync(x => x.Id);
        var customers = await _context.Customers
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name, x.Phone })
            .ToDictionaryAsync(x => x.Id);

        foreach (var debt in debts)
        {
            if (debt.PartyType == LookupCodes.PartyType.Farmer &&
                farmers.TryGetValue(debt.PartyId, out var farmer))
            {
                result[debt.Id] = new PartyInfo(farmer.Code, farmer.Name, farmer.Phone);
            }
            else if (debt.PartyType == LookupCodes.PartyType.Customer &&
                     customers.TryGetValue(debt.PartyId, out var customer))
            {
                result[debt.Id] = new PartyInfo(customer.Code, customer.Name, customer.Phone);
            }
        }

        return result;
    }

    private async Task<Dictionary<string, DocumentLabel>> ResolveDocumentLabelsAsync(
        List<(string? RefType, int? RefId)> references)
    {
        var result = new Dictionary<string, DocumentLabel>(StringComparer.OrdinalIgnoreCase);
        if (_context == null || references.Count == 0) return result;

        var receiptIds = references
            .Where(x => NormalizeRefType(x.RefType) == "PADDY_RECEIPT" && x.RefId.HasValue)
            .Select(x => x.RefId!.Value)
            .Distinct()
            .ToList();
        var outboundIds = references
            .Where(x => NormalizeRefType(x.RefType) == "OUTBOUND_ORDER" && x.RefId.HasValue)
            .Select(x => x.RefId!.Value)
            .Distinct()
            .ToList();
        var salesOrderIds = references
            .Where(x => NormalizeRefType(x.RefType) is "SALES_ORDER" or "SALES_ORDER_DEPOSIT" &&
                        x.RefId.HasValue)
            .Select(x => x.RefId!.Value)
            .Distinct()
            .ToList();
        var customerReturnIds = references
            .Where(x => NormalizeRefType(x.RefType) == "CUSTOMER_RETURN_ORDER" && x.RefId.HasValue)
            .Select(x => x.RefId!.Value)
            .Distinct()
            .ToList();

        var receipts = await _context.PaddyPurchaseReceipts
            .AsNoTracking()
            .Where(x => receiptIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.ReceiptCode })
            .ToListAsync();
        foreach (var receipt in receipts)
            result[BuildRefKey("PADDY_RECEIPT", receipt.Id)] =
                new DocumentLabel(receipt.ReceiptCode, $"/admin/rice-purchase?receiptId={receipt.Id}");

        var outboundOrders = await _context.OutboundOrders
            .AsNoTracking()
            .Where(x => outboundIds.Contains(x.Id) && !x.IsDeleted)
            .Join(_context.SalesOrders.AsNoTracking(),
                outbound => outbound.SalesOrderId,
                sales => sales.Id,
                (outbound, sales) => new { outbound.Id, sales.SOCode })
            .ToListAsync();
        foreach (var outbound in outboundOrders)
            result[BuildRefKey("OUTBOUND_ORDER", outbound.Id)] =
                new DocumentLabel($"{outbound.SOCode}-PX{outbound.Id}", $"/admin/outbound-orders?id={outbound.Id}");

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(x => salesOrderIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.SOCode })
            .ToListAsync();
        foreach (var salesOrder in salesOrders)
        {
            result[BuildRefKey("SALES_ORDER", salesOrder.Id)] =
                new DocumentLabel(salesOrder.SOCode, $"/admin/sales-orders?id={salesOrder.Id}");
            result[BuildRefKey("SALES_ORDER_DEPOSIT", salesOrder.Id)] =
                new DocumentLabel($"{salesOrder.SOCode} (đặt cọc)", $"/admin/sales-orders?id={salesOrder.Id}");
        }

        var returns = await _context.CustomerReturnOrders
            .AsNoTracking()
            .Where(x => customerReturnIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.ReturnCode })
            .ToListAsync();
        foreach (var order in returns)
            result[BuildRefKey("CUSTOMER_RETURN_ORDER", order.Id)] =
                new DocumentLabel(order.ReturnCode, $"/admin/customer-returns?id={order.Id}");

        result[BuildRefKey("OPENING_BALANCE", null)] =
            new DocumentLabel("SỐ DƯ ĐẦU KỲ", null);
        result[BuildRefKey("BALANCE_ADJUSTMENT", null)] =
            new DocumentLabel("SỐ DƯ CHƯA ĐỐI SOÁT", null);
        return result;
    }

    private static IEnumerable<DebtDocumentDto> ApplyDocumentSort(
        IEnumerable<DebtDocumentDto> rows,
        DebtDocumentDTParameters parameters)
    {
        var order = parameters.Order?.FirstOrDefault();
        var column = order != null &&
                     parameters.Columns != null &&
                     order.Column >= 0 &&
                     order.Column < parameters.Columns.Length
            ? parameters.Columns[order.Column].Data
            : "dueDate";
        var ascending = order?.Dir == DTOrderDir.ASC;
        Func<DebtDocumentDto, object> selector = column switch
        {
            "partyName" => x => x.PartyName,
            "documentCode" => x => x.DocumentCode,
            "totalAmount" => x => x.TotalAmount,
            "paidAmount" => x => x.PaidAmount,
            "outstandingAmount" => x => x.OutstandingAmount,
            "status" => x => x.Status,
            "transactionDate" => x => x.TransactionDate,
            _ => x => x.DueDate ?? DateTime.MaxValue
        };
        return ascending ? rows.OrderBy(selector) : rows.OrderByDescending(selector);
    }

    private static IEnumerable<DebtTransactionListItemDto> ApplyTransactionSort(
        IEnumerable<DebtTransactionListItemDto> rows,
        DebtTransactionDTParameters parameters)
    {
        var order = parameters.Order?.FirstOrDefault();
        var ascending = order?.Dir == DTOrderDir.ASC;
        return ascending
            ? rows.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id);
    }

    private static string? NormalizeDirection(string? direction)
    {
        var normalized = (direction ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is LookupCodes.DebtDirection.Payable or LookupCodes.DebtDirection.Receivable
            ? normalized
            : null;
    }

    private static string? NormalizeRefType(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string BuildRefKey(string? refType, int? refId)
        => $"{NormalizeRefType(refType) ?? "NO_REF"}:{refId?.ToString() ?? "NULL"}";

    private static string BuildFallbackDocumentCode(string? refType, int? refId)
    {
        var type = NormalizeRefType(refType);
        if (type == "OPENING_BALANCE") return "SỐ DƯ ĐẦU KỲ";
        if (type == "BALANCE_ADJUSTMENT") return "SỐ DƯ CHƯA ĐỐI SOÁT";
        if (string.IsNullOrWhiteSpace(type)) return "GIAO DỊCH THỦ CÔNG";
        return refId.HasValue ? $"{type}-{refId.Value}" : type;
    }

    private static bool ContainsIgnoreCase(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private sealed record PartyInfo(string Code, string Name, string? Phone);
    private sealed record DocumentLabel(string Code, string? Url);

    private static IEnumerable<PartyDebtDetailDto> ApplySort(
        IEnumerable<PartyDebtDetailDto> rows,
        PartyDebtDTParameters parameters)
    {
        var order = parameters.Order?.FirstOrDefault();
        var columnName = order != null &&
                         parameters.Columns != null &&
                         order.Column >= 0 &&
                         order.Column < parameters.Columns.Length
            ? parameters.Columns[order.Column].Data
            : "currentBalance";
        var ascending = order?.Dir == DTOrderDir.ASC;

        Func<PartyDebtDetailDto, object> selector = columnName switch
        {
            "partyCode" => x => x.PartyCode,
            "partyName" => x => x.PartyName,
            "direction" => x => x.Direction,
            "overdueAmount" => x => x.OverdueAmount,
            "creditLimit" => x => x.CreditLimit ?? 0m,
            "lastModifiedDate" => x => x.LastModifiedDate ?? x.CreatedDate,
            _ => x => x.CurrentBalance
        };

        return ascending
            ? rows.OrderBy(selector)
            : rows.OrderByDescending(selector);
    }

    private async Task<ApiResponse> ApplyTransactionWithRepositoriesAsync(
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
        if (!isDebit && dto.Amount > balanceBefore)
        {
            return ApiResponse.UnprocessableEntity(
                $"Số tiền thanh toán ({dto.Amount:N0} VNĐ) vượt quá dư nợ hiện tại ({balanceBefore:N0} VNĐ). " +
                "Vui lòng kiểm tra lại.");
        }

        debt.CurrentBalance = isDebit
            ? balanceBefore + dto.Amount
            : balanceBefore - dto.Amount;
        debt.UpdatedBy = dto.CreatedBy;
        debt.LastModifiedDate = now;
        await _partyDebtRepository.UpdateAsync(debt);

        var tx = new DebtTransaction
        {
            PartyDebtId = dto.PartyDebtId,
            TransactionType = transactionType,
            Amount = dto.Amount,
            BalanceAfter = debt.CurrentBalance,
            RefType = dto.RefType?.Trim().ToUpperInvariant(),
            RefId = dto.RefId,
            TransactionDate = dto.TransactionDate,
            DueDate = dto.DueDate,
            Note = dto.Note?.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = now
        };

        await _debtTransactionRepository.CreateAsync(tx);
        await _debtTransactionRepository.SaveChangesAsync();

        try
        {
            _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(
                service => service.EvaluatePartyDebtAsync(debt.Id, CancellationToken.None));
        }
        catch
        {
            // Không để lỗi hệ thống cảnh báo làm hỏng giao dịch công nợ.
        }

        return ApiResponse.Success(
            ToTransactionDto(tx),
            isDebit ? "Ghi phát sinh nợ thành công." : "Đã lưu thanh toán.");
    }

    private static DebtTransactionDetailDto ToTransactionDto(DebtTransaction tx)
        => new()
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

}
