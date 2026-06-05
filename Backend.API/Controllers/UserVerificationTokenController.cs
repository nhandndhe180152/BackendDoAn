using Asp.Versioning;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _userVerificationTokenService.GetPagedAsync(parameters);

            return BaseResult(result);
        }
    }
}
