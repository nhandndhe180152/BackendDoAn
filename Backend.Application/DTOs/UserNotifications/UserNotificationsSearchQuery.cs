using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.UserNotifications;

public class UserNotificationsSearchQuery : SearchQuery
{
    public bool? IsRead { get; set; }
}
