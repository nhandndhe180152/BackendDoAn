using System;

namespace Backend.Application.DTOs.Locations;

public class UpdateLocationDto : CreateLocationDto
{
    public int Id { get; set; }
}
