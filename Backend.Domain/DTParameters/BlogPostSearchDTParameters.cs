using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class BlogPostSearchDTParameters : SearchQuery
{
    public int? BlogPostCategoryId { get; set; } // Lọc theo danh mục
    public List<int> BlogPostTagIds { get; set; } = new List<int>(); // Lọc theo tag
}
