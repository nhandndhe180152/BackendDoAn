using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/audit-log")]
    [ApiController]
    public class AuditLogController : BaseController
    {
        private readonly IAuditLogService _auditLogService;
        public AuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }
        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.AUDIT_LOGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] AuditLogDTParameters parameters)
        {
            var result = await _auditLogService.GetPagedAsync(parameters);
            return BaseResult(result);
        }
        [HttpPost("me")]
        [CustomAuthorize(Enums.Menu.AUDIT_LOGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedByUserIdAsync([FromBody] AuditLogDTParameters parameters)
        {
            parameters.UserId = this.GetLoggedInUserId();
            var result = await _auditLogService.GetPagedAsync(parameters);
            return BaseResult(result);
        }
        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.AUDIT_LOGS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _auditLogService.GetByIdAsync(id);
            return BaseResult(data);
        }

        [HttpGet("actions")]
        [CustomAuthorize(Enums.Menu.AUDIT_LOGS, Enums.Action.READ)]
        public IActionResult GetListAction()
        {
            var result = _auditLogService.GetListAction();

            return BaseResult(result);
        }

        [HttpGet("audit-entities")]
        [CustomAuthorize(Enums.Menu.AUDIT_LOGS, Enums.Action.READ)]
        public IActionResult GetListAuditEntity()
        {
            var result = _auditLogService.GetListAuditEntity();

            return BaseResult(result);
        }
    }
}
