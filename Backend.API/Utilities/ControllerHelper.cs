using System;
using System.Security.Claims;
using Backend.Share.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Utilities;

public static class ControllerHelper
{
    /// <summary>
    /// Return logged in user info
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string? GetLoggedInUserInfo(this ControllerBase controller, string key)
    {
        try
        {
            if (controller.HttpContext.User.Identity is ClaimsIdentity identity)
            {
                return identity.FindFirst(key)?.Value;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int GetLoggedInUserId(this ControllerBase controller)
    {
        return Convert.ToInt32(GetLoggedInUserInfo(controller, ClaimNames.ID));
    }
    public static int? GetLoggedInOfficeId(this ControllerBase controller)
    {
        var officeId = GetLoggedInUserInfo(controller, ClaimNames.OFFICE_ID);
        return string.IsNullOrEmpty(officeId) ? null : Convert.ToInt32(officeId);
    }
    public static List<int> GetLoggedInRoleIds(this ControllerBase controller)
    {
        var roleIds = GetLoggedInUserInfo(controller, ClaimNames.ROLE_IDS);
        return string.IsNullOrEmpty(roleIds) ? new List<int>() : roleIds.Split(",").Select(x => int.Parse(x)).ToList();
    }
}
