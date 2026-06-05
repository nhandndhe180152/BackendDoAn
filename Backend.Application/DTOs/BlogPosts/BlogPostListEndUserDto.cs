using System;
using Backend.Application.DTOs.FileUploads;

namespace Backend.Application.DTOs.BlogPosts;

public class BlogPostListEndUserDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string SeoAlias { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime? PublicationDate { get; set; }
    public virtual FileUploadDetailDto? CoverImage { get; set; }

}