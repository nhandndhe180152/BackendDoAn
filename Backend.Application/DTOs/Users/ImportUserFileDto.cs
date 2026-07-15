using System;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.DTOs.Users;

public class ImportUserFileDto
{
    public IFormFile File { get; set; } = null!;
}
