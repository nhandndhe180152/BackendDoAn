using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class NotificationDTParameters : DTParameter
{
    public List<int> NotificationCategoryIds = new List<int>();
    public bool IsAdmin { get; set; } = false;
    public int UserId { get; set; }
}