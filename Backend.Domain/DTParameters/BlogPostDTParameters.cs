using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class BlogPostDTParameters : DTParameter
{
    public List<int> BlogPostStatusIds { get; set; } = new List<int>();
    public List<int> BlogPostCategoryIds { get; set; } = new List<int>();
    public List<int> BlogPostLayoutIds { get; set; } = new List<int>();
    public List<int> AuthorIds { get; set; } = new List<int>();
}
