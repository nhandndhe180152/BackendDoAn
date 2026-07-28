using System.Threading.Tasks;
using Backend.Application.DTOs.PartyDebts;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPartyDebtService
{
    Task<ApiResponse> GetByPartyAsync(string partyType, int partyId, int? organizationId);
    Task<ApiResponse> GetPagedAsync(PartyDebtDTParameters parameters);
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> GetDocumentsAsync(DebtDocumentDTParameters parameters);
    Task<ApiResponse> GetAllTransactionsAsync(DebtTransactionDTParameters parameters);
    Task<ApiResponse> GetTransactionsAsync(int partyDebtId, DTParameter parameters);
    Task<ApiResponse> ChargeAsync(CreateDebtTransactionDto dto);
    Task<ApiResponse> PaymentAsync(CreateDebtTransactionDto dto);
}
