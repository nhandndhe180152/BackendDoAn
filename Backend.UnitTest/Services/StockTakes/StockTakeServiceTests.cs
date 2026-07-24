using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.StockTakes;

[Trait("Service", "StockTake")]
public class StockTakeServiceTests
{
    private readonly Mock<IStockTakeRepository> _stockTakeRepo = new();
    private readonly Mock<IStockTakeItemRepository> _stockTakeItemRepo = new();
    private readonly Mock<IInventoryTransactionService> _invTxService = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private StockTakeService Sut() => new(
        _stockTakeRepo.Object, _stockTakeItemRepo.Object, _invTxService.Object,
        _invRepo.Object, _http.Object, _dispatcher.Object);

    [Fact]
    public async Task SoftDeleteAsync_NotFound_Returns404()
    {
        _stockTakeRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<StockTake, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<StockTake>().AsQueryable().BuildMock());

        var result = await Sut().SoftDeleteAsync(999);

        result.Status.Should().Be(404);
    }
}
