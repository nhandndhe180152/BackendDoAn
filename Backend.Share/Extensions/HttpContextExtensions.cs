using System;
using Backend.Share.Constants;
using Microsoft.AspNetCore.Http;

namespace Backend.Share.Extensions;

public static class HttpContextExtensions
{
    public static int GetCurrentUserId(this HttpContext context)
    {
        return Convert.ToInt32(context.User.FindFirst(x => x.Type == ClaimNames.ID)?.Value);
    }

    public static List<int> GetCurrentRoleIds(this HttpContext context)
    {
        var roleIds = context.User.FindFirst(x => x.Type == ClaimNames.ROLE_IDS)?.Value;
        return string.IsNullOrEmpty(roleIds) ? new List<int>() : roleIds.Split(",").Select(x => Convert.ToInt32(x)).ToList();
    }
}
