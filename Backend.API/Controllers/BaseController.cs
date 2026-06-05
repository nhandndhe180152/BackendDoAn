using System.Net;
using Backend.Application.Constants;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.API.Controllers
{
    public abstract class BaseController : Controller
    {
        [NonAction]
        protected IActionResult BaseResult(ApiResponse apiResponse)
        {
            switch (apiResponse.Status)
            {
                case (int)HttpStatusCode.OK:
                    return Ok(apiResponse);
                case (int)HttpStatusCode.NotFound:
                    return NotFound(apiResponse);
                case (int)HttpStatusCode.Unauthorized:
                    return Unauthorized(apiResponse);
                case (int)HttpStatusCode.BadRequest:
                    return BadRequest(apiResponse);
                case (int)HttpStatusCode.UnprocessableEntity:
                    return UnprocessableEntity(apiResponse);
                case (int)HttpStatusCode.Forbidden:
                    return new ObjectResult(apiResponse)
                    {
                        StatusCode = (int)HttpStatusCode.Forbidden,
                    };
                default:
                    return new ObjectResult(apiResponse)
                    {
                        StatusCode = apiResponse.Status,
                    };
            }
        }
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!ModelState.IsValid)//valid model state
            {
                var errors = ModelState
                    .Where(x => x.Value != null)
                    .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList())
                    .Select(x => new FormErrorMessage
                    {
                        Field = x.Key,
                        Message = x.Value
                    })
                    .ToList();
                if (errors.Any(x => string.IsNullOrEmpty(x.Field)))
                {
                    var apiCode = ApiCodeConstants.Common.BadRequest;
                    context.Result = UnprocessableEntity(ApiResponse.BadRequest(errors, ErrorMessagesConstants.GetMessage(apiCode), apiCode));
                }
                else
                {
                    var apiCode = ApiCodeConstants.Common.UnprocessableEntity;
                    context.Result = UnprocessableEntity(ApiResponse.UnprocessableEntity(errors, ErrorMessagesConstants.GetMessage(apiCode), apiCode));
                }

                //var errors = ModelState.Values.SelectMany(c => c.Errors).Select(c => c.ErrorMessage);
                //filterContext.Result = UnprocessableEntity(ApiResponse.Error(errors));
            }

            await base.OnActionExecutionAsync(context, next);
        }
    }
}
