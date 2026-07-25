using Asp.Versioning;
using Backend.API.Utilities;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiController]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/background-jobs")]
    public class BackgroundJobsController : BaseController
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public BackgroundJobsController(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }

        [HttpPost("trigger-low-stock")]
        [CustomAuthorize(Backend.Domain.Enums.Enums.Menu.SYSTEM_SETTINGS, Backend.Domain.Enums.Enums.Action.UPDATE)]
        public IActionResult TriggerLowStockJob()
        {
            _backgroundJobClient.Enqueue<Backend.Infrastructure.Services.LowStockDetectionJob>(x => x.ExecuteAsync());
            return Ok(new { Message = "Đã đưa JOB-01 (Low Stock Detection) vào hàng đợi Hangfire." });
        }

        [HttpPost("trigger-intake-bottleneck")]
        [CustomAuthorize(Backend.Domain.Enums.Enums.Menu.SYSTEM_SETTINGS, Backend.Domain.Enums.Enums.Action.UPDATE)]
        public IActionResult TriggerIntakeBottleneckJob()
        {
            _backgroundJobClient.Enqueue<Backend.Infrastructure.Services.IntakeBottleneckEvaluationJob>(x => x.ExecuteAsync());
            return Ok(new { Message = "Đã đưa JOB-02 (Intake Bottleneck Evaluation) vào hàng đợi Hangfire." });
        }

        [HttpPost("trigger-lot-quality-recheck")]
        [CustomAuthorize(Backend.Domain.Enums.Enums.Menu.SYSTEM_SETTINGS, Backend.Domain.Enums.Enums.Action.UPDATE)]
        public IActionResult TriggerLotQualityRecheckJob()
        {
            _backgroundJobClient.Enqueue<Backend.Infrastructure.Services.LotQualityRecheckJob>(x => x.ExecuteAsync());
            return Ok(new { Message = "Đã đưa JOB-03 (Lot Quality Recheck) vào hàng đợi Hangfire." });
        }

        [HttpPost("trigger-debt-due-overdue")]
        [CustomAuthorize(Backend.Domain.Enums.Enums.Menu.SYSTEM_SETTINGS, Backend.Domain.Enums.Enums.Action.UPDATE)]
        public IActionResult TriggerDebtDueAndOverdueReminderJob()
        {
            _backgroundJobClient.Enqueue<Backend.Infrastructure.Services.DebtDueAndOverdueReminderJob>(x => x.ExecuteAsync());
            return Ok(new { Message = "Đã đưa JOB-04 (Debt Due & Overdue Reminder) vào hàng đợi Hangfire." });
        }

        [HttpPost("trigger-fcm-notification-retry")]
        [CustomAuthorize(Backend.Domain.Enums.Enums.Menu.SYSTEM_SETTINGS, Backend.Domain.Enums.Enums.Action.UPDATE)]
        public IActionResult TriggerFcmNotificationRetryJob()
        {
            _backgroundJobClient.Enqueue<Backend.Infrastructure.Services.FcmNotificationRetryJob>(x => x.ExecuteAsync());
            return Ok(new { Message = "Đã đưa JOB-05 (FCM Notification Retry) vào hàng đợi Hangfire." });
        }
    }
}
