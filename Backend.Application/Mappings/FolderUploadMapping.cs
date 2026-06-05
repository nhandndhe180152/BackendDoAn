using System;
using Backend.Application.DTOs.FolderUploads;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class FolderUploadMapping
{
    public static FolderUpload ToEntity(this CreateFolderUploadDto dto)
    {
        return new FolderUpload
        {
            CreatedBy = dto.CreatedBy,
            FolderName = dto.FolderName,
            CreatedDate = DateTime.Now,
            ParentId = dto.ParentId != 0 ? dto.ParentId : null,
            TreeIds = string.Empty,
            FolderPath = string.Empty,
        };
    }

    public static FolderUpload ToEntity(this UpdateFolderUploadDto dto, FolderUpload existData)
    {
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        existData.FolderName = dto.FolderName;

        return existData;
    }
}
