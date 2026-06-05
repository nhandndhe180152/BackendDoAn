using System;
using Backend.Share.Entities;

namespace Backend.Domain.Aggregates;

public class BlogPostAggregate
    {
        public int Id { get; set; }
        public int BlogPostCategoryId { get; set; }
        public string BlogPostCategoryName { get; set; } = null!;
        public string BlogPostCategoryColor { get; set; } = null!;
        public int BlogLayoutId { get; set; }
        public string BlogLayoutName { get; set; } = null!;
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = null!;
        public int? CoverImageId { get; set; }
        public string? CoverImageUrl { get; set; }
        public string Title { get; set; } = null!;
        public string SeoAlias { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Content { get; set; } = null!;
        public int BlogPostStatusId { get; set; }
        public string BlogPostStatusName { get; set; } = null!;
        public string BlogPostStatusColor { get; set; } = null!;
        public bool IsApproved { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? PublicationDate { get; set; }
        public bool IsShowOnHomePage { get; set; }
        public bool IsPopular { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<DataItem<int>> BlogPostTagIds { get; set; } = new List<DataItem<int>>();
    }