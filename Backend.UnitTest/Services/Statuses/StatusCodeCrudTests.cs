using System.Threading.Tasks;
using Backend.Application.DTOs.InboundOrderStatuses;
using Backend.Application.DTOs.OutboundOrderStatuses;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
using Backend.Application.DTOs.SalesOrderStatuses;
using Backend.Application.DTOs.StockTransferStatuses;
using Backend.Application.Mappings;
using Backend.Application.Validators.InboundOrderStatuses;
using Backend.Application.Validators.OutboundOrderStatuses;
using Backend.Application.Validators.PurchaseOrderStatuses;
using Backend.Application.Validators.ReturnToSupplierOrderStatuses;
using Backend.Application.Validators.SalesOrderStatuses;
using Backend.Application.Validators.StockTransferStatuses;
using Backend.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Backend.UnitTest.Services.Statuses;

[Trait("Service", "StatusCodeCrud")]
public class StatusCodeCrudTests
{
    [Fact]
    public void CreateMappings_NormalizeCode_ForAllStatusTypes()
    {
        new CreateInboundOrderStatusDto { Code = " custom_in ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<InboundOrderStatus>(x => x.Code == "CUSTOM_IN" && x.Name == "Mới" && x.Color == "#fff");
        new CreateOutboundOrderStatusDto { Code = " custom_out ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<OutboundOrderStatus>(x => x.Code == "CUSTOM_OUT" && x.Name == "Mới" && x.Color == "#fff");
        new CreatePurchaseOrderStatusDto { Code = " custom_po ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<PurchaseOrderStatus>(x => x.Code == "CUSTOM_PO" && x.Name == "Mới" && x.Color == "#fff");
        new CreateSalesOrderStatusDto { Code = " custom_so ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<SalesOrderStatus>(x => x.Code == "CUSTOM_SO" && x.Name == "Mới" && x.Color == "#fff");
        new CreateStockTransferStatusDto { Code = " custom_st ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<StockTransferStatus>(x => x.Code == "CUSTOM_ST" && x.Name == "Mới" && x.Color == "#fff");
        new CreateReturnToSupplierOrderStatusDto { Code = " custom_rts ", Name = " Mới ", Color = " #fff " }
            .ToEntity().Should().Match<ReturnToSupplierOrderStatus>(x => x.Code == "CUSTOM_RTS" && x.Name == "Mới" && x.Color == "#fff");
    }

    [Fact]
    public void UpdateMappings_PreserveCode_ForAllStatusTypes()
    {
        new UpdateInboundOrderStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new InboundOrderStatus { Code = "DRAFT" }).Code.Should().Be("DRAFT");
        new UpdateOutboundOrderStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new OutboundOrderStatus { Code = "DRAFT" }).Code.Should().Be("DRAFT");
        new UpdatePurchaseOrderStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new PurchaseOrderStatus { Code = "DRAFT" }).Code.Should().Be("DRAFT");
        new UpdateSalesOrderStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new SalesOrderStatus { Code = "NEW" }).Code.Should().Be("NEW");
        new UpdateStockTransferStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new StockTransferStatus { Code = "DRAFT" }).Code.Should().Be("DRAFT");
        new UpdateReturnToSupplierOrderStatusDto { Name = "Tên mới", Color = "#000" }.ToEntity(new ReturnToSupplierOrderStatus { Code = "DRAFT" }).Code.Should().Be("DRAFT");
    }

    [Fact]
    public async Task CreateValidators_RejectInvalidCode_ForAllStatusTypes()
    {
        (await new CreateInboundOrderStatusDtoValidator().ValidateAsync(new CreateInboundOrderStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
        (await new CreateOutboundOrderStatusDtoValidator().ValidateAsync(new CreateOutboundOrderStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
        (await new CreatePurchaseOrderStatusDtoValidator().ValidateAsync(new CreatePurchaseOrderStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
        (await new CreateSalesOrderStatusDtoValidator().ValidateAsync(new CreateSalesOrderStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
        (await new CreateStockTransferStatusDtoValidator().ValidateAsync(new CreateStockTransferStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
        (await new CreateReturnToSupplierOrderStatusDtoValidator().ValidateAsync(new CreateReturnToSupplierOrderStatusDto { Code = "mã lỗi", Name = "Tên", Color = "#fff" })).IsValid.Should().BeFalse();
    }
}
