using System;
using Backend.Application.DTOs.FolderUploads;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IFolderUploadService : IServiceBase<int, CreateFolderUploadDto, UpdateFolderUploadDto, DTParameter>
{
}
