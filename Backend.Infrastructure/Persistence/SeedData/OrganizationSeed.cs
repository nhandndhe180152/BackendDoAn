using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class OrganizationSeed
{
    public static IEnumerable<Organization> GetOrganizations()
    {
        return new[]
        {
            new Organization
            {
                Id = 1,
                Code = "TUANMAY",
                Name = "Cơ sở kinh doanh lúa gạo Tuấn Mây",
                Description = "Hộ kinh doanh kiêm xay xát lúa gạo",
                TaxCode = null,
                Address = null,
                ContactEmail = null,
                ContactPhone = null,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1)
            }
        };
    }
}
