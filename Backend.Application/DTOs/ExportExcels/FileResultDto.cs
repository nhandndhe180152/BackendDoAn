using System;

namespace Backend.Application.DTOs.ExportExcels;

public class FileResultDto
{
    public byte[] Bytes { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
