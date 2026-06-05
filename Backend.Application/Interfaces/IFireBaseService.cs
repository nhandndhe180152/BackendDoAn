using System;

namespace Backend.Application.Interfaces;

public interface IFireBaseService
{
    Task SendNotificationAsync(List<string> deviceTokens, string title, string body, string? categoryId, string? directionId);
}
