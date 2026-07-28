using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PartyDebts;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Sổ công nợ 2 chiều (PartyDebt).
    /// Hỗ trợ: FARMER (phải trả), CUSTOMER/SUPPLIER (phải thu/trả).
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/party-debts")]
    [Authorize]
    [ApiController]
    public class PartyDebtController : BaseController
    {
        private readonly IPartyDebtService _partyDebtService;

        public PartyDebtController(IPartyDebtService partyDebtService)
        {
            _partyDebtService = partyDebtService;
        }

        /// <summary>GET /party-debts/party?partyType=FARMER&amp;partyId=1</summary>
        [HttpGet("party")]
        public async Task<IActionResult> GetByPartyAsync(
            [FromQuery] string partyType,
            [FromQuery] int partyId,
            [FromQuery] int? organizationId)
        {
            var result = await _partyDebtService.GetByPartyAsync(partyType, partyId, organizationId);
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/paged-advanced — danh sách phân trang</summary>
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] PartyDebtDTParameters parameters)
        {
            var result = await _partyDebtService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        /// <summary>GET /party-debts/summary — tổng quan phải thu, phải trả và quá hạn.</summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryAsync()
        {
            var result = await _partyDebtService.GetSummaryAsync();
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/documents/paged — công nợ chi tiết theo chứng từ nguồn.</summary>
        [HttpPost("documents/paged")]
        public async Task<IActionResult> GetDocumentsAsync([FromBody] DebtDocumentDTParameters parameters)
        {
            var result = await _partyDebtService.GetDocumentsAsync(parameters);
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/transactions/paged-advanced — lịch sử giao dịch toàn hệ thống.</summary>
        [HttpPost("transactions/paged-advanced")]
        public async Task<IActionResult> GetAllTransactionsAsync([FromBody] DebtTransactionDTParameters parameters)
        {
            var result = await _partyDebtService.GetAllTransactionsAsync(parameters);
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/{id}/transactions/paged — lịch sử giao dịch</summary>
        [HttpPost("{id}/transactions/paged")]
        public async Task<IActionResult> GetTransactionsAsync(int id, [FromBody] DTParameter parameters)
        {
            var result = await _partyDebtService.GetTransactionsAsync(id, parameters);
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/charge — ghi phát sinh nợ thủ công</summary>
        [HttpPost("charge")]
        public async Task<IActionResult> ChargeAsync([FromBody] CreateDebtTransactionDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _partyDebtService.ChargeAsync(dto);
            return BaseResult(result);
        }

        /// <summary>POST /party-debts/payment — ghi thanh toán thủ công</summary>
        [HttpPost("payment")]
        public async Task<IActionResult> PaymentAsync([FromBody] CreateDebtTransactionDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _partyDebtService.PaymentAsync(dto);
            return BaseResult(result);
        }
    }
}
