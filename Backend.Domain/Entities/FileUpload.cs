using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class FileUpload : EntityAuditBase<int>
    {
        public int FolderUploadId { get; set; }
        public string FileName { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public long FileSize { get; set; }
        public string FileKey { get; set; } = null!;
        [JsonIgnore]
        public virtual ICollection<User> Users { get; set; }
        [JsonIgnore]
        public virtual ICollection<BlogPost> BlogPosts { get; set; }
        [JsonIgnore]
        public virtual FolderUpload FolderUpload { get; set; }  
    }
