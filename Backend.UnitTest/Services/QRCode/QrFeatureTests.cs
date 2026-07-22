using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.QrCode;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.QRCode
{
    public class QrFeatureTests
    {
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

        private BackendContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BackendContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new BackendContext(options);
        }

        private QrIdentifierService CreateQrIdentifierService(BackendContext context)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["UserId"] = 1;
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            var storageMock = new Mock<Application.Interfaces.IStorageService>();
            storageMock.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync((IFormFile file, string folder) => new Backend.Share.Entities.FileUploadResult
                {
                    Success = true,
                    FilePath = $"https://cloudinary.com/fake/{file.FileName}"
                });

            return new QrIdentifierService(context, _httpContextAccessorMock.Object, storageMock.Object);
        }

        private QRCodeService CreateQRCodeService(BackendContext context, QrIdentifierService qrIdentifierService)
        {
            var productVariantRepoMock = new Mock<Domain.Interfaces.Repositories.IProductVariantRepository>();
            var storageServiceMock = new Mock<Application.Interfaces.IStorageService>();
            var inventoryRepoMock = new Mock<Domain.Interfaces.Repositories.IInventoryRepository>();

            return new QRCodeService(
                productVariantRepoMock.Object,
                storageServiceMock.Object,
                inventoryRepoMock.Object,
                context,
                qrIdentifierService);
        }

        private async Task SeedBaseDataAsync(BackendContext context)
        {
            // Warehouse
            var wh = new Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Kho Chính", IsActive = true, IsDeleted = false };
            context.Warehouses.Add(wh);

            // User Role Admin
            var ur = new UserRole { Id = 1, UserId = 1, RoleId = 1001, IsDeleted = false };
            context.UserRoles.Add(ur);

            // LotStatuses
            var normalStatus = new LotStatus { Id = 1, Name = "Bình thường", Code = LotStatusCodeConstants.InStock, IsSellable = true, Color = "#10B981" };
            var quarantineStatus = new LotStatus { Id = 2, Name = "Cách ly", Code = LotStatusCodeConstants.Quarantine, IsSellable = false, Color = "#EF4444" };
            context.LotStatuses.AddRange(normalStatus, quarantineStatus);

            // Product Variant
            var pv = new ProductVariant { Id = 1, SKU = "PV-01", Name = "Gạo thơm ST25", IsActive = true, IsDeleted = false };
            context.ProductVariants.Add(pv);

            // PaddyLot
            var lot = new Domain.Entities.PaddyLot
            {
                Id = 1,
                LotCode = "LOT-01",
                LotType = "PADDY",
                WarehouseId = 1,
                ProductVariantId = 1,
                StatusId = 1,
                InboundDate = DateTime.UtcNow,
                InitialWeightKg = 1000m,
                RemainingWeightKg = 1000m,
                IsDeleted = false
            };
            context.PaddyLots.Add(lot);

            // Location
            var loc = new Domain.Entities.Location
            {
                Id = 1,
                WarehouseId = 1,
                ZoneName = "ZoneA",
                ShelfRow = "R1",
                ShelfLevel = "L1",
                SlotCode = "S1",
                IsActive = true,
                IsQuarantine = false,
                MaxCapacity = 5000m,
                CurrentOccupancy = 0m,
                IsDeleted = false
            };
            context.Locations.Add(loc);

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task EnsurePaddyLotQrCodeAsync_CorrectlyGeneratesUniqueIdentifier()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var service = CreateQrIdentifierService(context);

            // Act
            var result = await service.EnsurePaddyLotQrCodeAsync(1, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.QrCode.Should().StartWith("PL-");
            result.QrPayload.Should().Be($"STOCKLITE|1|PADDY_LOT|{result.QrCode}");
            result.QrImageUrl.Should().Contain("fake/paddylot-qr-1.png");
            result.LabelUrl.Should().Be($"/api/v1/paddy-lots/{result.EntityId}/label");

            var lot = await context.PaddyLots.FindAsync(1);
            lot.Should().NotBeNull();
            lot!.QrCode.Should().Be(result.QrCode);
        }

        [Fact]
        public async Task RegeneratePaddyLotQrCodeAsync_ChangesCodeAndLogsActivity()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var service = CreateQrIdentifierService(context);

            var original = await service.EnsurePaddyLotQrCodeAsync(1, CancellationToken.None);

            // Act
            var regenerated = await service.RegeneratePaddyLotQrCodeAsync(1, "Tem bị rách", CancellationToken.None);

            // Assert
            regenerated.Should().NotBeNull();
            regenerated.QrCode.Should().NotBe(original.QrCode);
            regenerated.QrCode.Should().StartWith("PL-");

            var lot = await context.PaddyLots.FindAsync(1);
            lot!.QrCode.Should().Be(regenerated.QrCode);

            var log = await context.AuditLogs.FirstOrDefaultAsync(l => l.TargetId == "1" && l.TargetType == "PaddyLot");
            log.Should().NotBeNull();
            log!.Description.Should().Contain("mã QR");
            log.Description.Should().Contain("Tem bị rách");
        }

        [Fact]
        public async Task ResolveQrAsync_PaddyLot_ParsesCorrectly()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var ensureRes = await idService.EnsurePaddyLotQrCodeAsync(1, CancellationToken.None);

            var request = new QrResolveRequestDto { Payload = ensureRes.QrPayload };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.EntityType.Should().Be("PADDY_LOT");
            response.EntityId.Should().Be(1);
            response.DisplayCode.Should().Be("LOT-01");
            response.RemainingWeightKg.Should().Be(1000m);
            response.IsQuarantined.Should().BeFalse();
        }

        [Fact]
        public async Task ResolveQrAsync_Location_ParsesCorrectly()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var ensureRes = await idService.EnsureLocationQrCodeAsync(1, CancellationToken.None);

            var request = new QrResolveRequestDto { Payload = ensureRes.QrPayload };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.EntityType.Should().Be("LOCATION");
            response.EntityId.Should().Be(1);
            response.DisplayCode.Should().Contain("ZoneA");
            response.IsQuarantine.Should().BeFalse();
        }

        [Fact]
        public async Task ResolveQrAsync_ContextChecking_StoreIn_WrongWarehouse_Fails()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var ensureRes = await idService.EnsureLocationQrCodeAsync(1, CancellationToken.None);

            var request = new QrResolveRequestDto
            {
                Payload = ensureRes.QrPayload,
                Context = new QrContextDto
                {
                    Operation = "STORE_IN",
                    WarehouseId = 999
                }
            };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.ValidationResult.Should().NotBeNull();
            response.ValidationResult!.Success.Should().BeFalse();
            response.ValidationResult.ErrorCode.Should().Be("LOCATION_WRONG_WAREHOUSE");
        }

        [Fact]
        public async Task ResolveQrAsync_ContextChecking_StoreIn_QuarantineMismatch_Fails()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var ensureRes = await idService.EnsureLocationQrCodeAsync(1, CancellationToken.None);

            // Setup quarantined PaddyLot
            var qLot = await context.PaddyLots.FindAsync(1);
            qLot!.StatusId = 2; // Quarantine status
            await context.SaveChangesAsync();

            var request = new QrResolveRequestDto
            {
                Payload = ensureRes.QrPayload,
                Context = new QrContextDto
                {
                    Operation = "STORE_IN",
                    WarehouseId = 1,
                    ReferenceId = 1
                }
            };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.ValidationResult.Should().NotBeNull();
            response.ValidationResult!.Success.Should().BeFalse();
            response.ValidationResult.ErrorCode.Should().Be("LOCATION_QUARANTINE_REQUIRED");
        }

        [Fact]
        public async Task ResolveQrAsync_ContextChecking_MillingInput_QuarantinedLot_Fails()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var lot = await context.PaddyLots.FindAsync(1);
            lot!.StatusId = 2; // Quarantine status
            await context.SaveChangesAsync();

            var ensureRes = await idService.EnsurePaddyLotQrCodeAsync(1, CancellationToken.None);

            var request = new QrResolveRequestDto
            {
                Payload = ensureRes.QrPayload,
                Context = new QrContextDto
                {
                    Operation = "MILLING_INPUT"
                }
            };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.ValidationResult.Should().NotBeNull();
            response.ValidationResult!.Success.Should().BeFalse();
            response.ValidationResult.ErrorCode.Should().Be("LOT_QUARANTINED");
        }

        [Fact]
        public async Task ResolveQrAsync_ContextChecking_OutboundPicking_ProductMismatch_Fails()
        {
            // Arrange
            using var context = CreateContext();
            await SeedBaseDataAsync(context);
            var idService = CreateQrIdentifierService(context);
            var qrService = CreateQRCodeService(context, idService);

            var ensureRes = await idService.EnsurePaddyLotQrCodeAsync(1, CancellationToken.None);

            var request = new QrResolveRequestDto
            {
                Payload = ensureRes.QrPayload,
                Context = new QrContextDto
                {
                    Operation = "OUTBOUND_PICKING",
                    ReferenceId = 999
                }
            };

            // Act
            var response = await qrService.ResolveQrAsync(request);

            // Assert
            response.ValidationResult.Should().NotBeNull();
            response.ValidationResult!.Success.Should().BeFalse();
            response.ValidationResult.ErrorCode.Should().Be("LOT_PRODUCT_MISMATCH");
        }
    }
}
