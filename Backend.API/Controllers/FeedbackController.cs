using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Feedbacks;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/feedback")]
    [ApiController]
    public class FeedbackController : BaseController, IBaseController<int, CreateFeedbackDto, UpdateFeedbackDto, DTParameter>
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateFeedbackDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId() == 0 ? null : this.GetLoggedInUserId();
            var result = await _feedbackService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _feedbackService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _feedbackService.GetByIdAsync(id);

            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _feedbackService.GetPagedAsync(query);

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            throw new NotImplementedException();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _feedbackService.SoftDeleteAsync(id);

            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateFeedbackDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _feedbackService.UpdateAsync(obj);

            return BaseResult(result);
        }
    }
}
