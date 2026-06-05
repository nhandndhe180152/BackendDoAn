using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Tags;

public class TagSearchQuery : SearchQuery
{
    public int? TagTypeId { get; set; }
}
