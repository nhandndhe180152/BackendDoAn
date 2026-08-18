using Asp.Versioning;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.API.Utilities;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/user-verification-token")]
    [ApiController]
    public class UserVerificationTokenController : BaseController
    {
        private readonly IUserVerificationTokenService _userVerificationTokenService;

        public UserVerificationTokenController(IUserVerificationTokenService userVerificationTokenService)
        {
            _userVerificationTokenService = userVerificationTokenService;
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.USER_VERIFICATION_TOKEN, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _userVerificationTokenService.GetPagedAsync(parameters);

            return BaseResult(result);
        }
    }
}
