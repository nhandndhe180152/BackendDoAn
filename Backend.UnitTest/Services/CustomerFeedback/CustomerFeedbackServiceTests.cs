using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.CustomerFeedbacks;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Backend.Domain.Enums;
using Backend.Domain.Entities;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using MockQueryable.Moq;
using Xunit;

namespace Backend.UnitTest.Services.CustomerFeedback
{
    public class CustomerFeedbackServiceTests
    {
        private readonly Mock<IApplicationDbContext> _contextMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<IPaddyLotTraceabilityService> _traceabilityServiceMock;
        private readonly CustomerFeedbackService _service;

        public CustomerFeedbackServiceTests()
        {
            _contextMock = new Mock<IApplicationDbContext>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _traceabilityServiceMock = new Mock<IPaddyLotTraceabilityService>();

            var context = new DefaultHttpContext();
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

            _service = new CustomerFeedbackService(
                _contextMock.Object,
                _httpContextAccessorMock.Object,
                _traceabilityServiceMock.Object
            );
        }

        [Fact]
        public async Task CreateAsync_InvalidFeedbackType_ReturnsBadRequest()
        {
            var dto = new CreateCustomerFeedbackDto
            {
                FeedbackType = "INVALID_TYPE"
            };

            var result = await _service.CreateAsync(dto);

            Assert.Equal((int)System.Net.HttpStatusCode.BadRequest, result.Status);
            Assert.Contains("Loại feedback không hợp lệ", result.Message);
        }

        [Fact]
        public async Task CreateAsync_OutboundOrderNotFound_ReturnsNotFound()
        {
            var dto = new CreateCustomerFeedbackDto
            {
                FeedbackType = CustomerFeedbackType.Quality,
                OutboundOrderId = 99
            };

            _contextMock.Setup(c => c.OutboundOrders)
                .Returns(new List<OutboundOrder>().AsQueryable().BuildMockDbSet().Object);

            var result = await _service.CreateAsync(dto);

            Assert.Equal((int)System.Net.HttpStatusCode.NotFound, result.Status);
            Assert.Contains("Không tìm thấy phiếu xuất kho", result.Message);
        }

        [Fact]
        public async Task CreateAsync_Success_ReturnsCreated()
        {
            var dto = new CreateCustomerFeedbackDto
            {
                FeedbackType = CustomerFeedbackType.Quality,
                SalesOrderId = 1,
                OutboundOrderId = 1,
                Description = "Gạo mốc",
                Severity = "High"
            };

            var outbound = new OutboundOrder
            {
                Id = 1,
                SalesOrderId = 1,
                OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Completed }
            };

            _contextMock.Setup(c => c.OutboundOrders)
                .Returns(new List<OutboundOrder> { outbound }.AsQueryable().BuildMockDbSet().Object);

            var mockDbSet = new Mock<DbSet<Backend.Domain.Entities.CustomerFeedback>>();
            _contextMock.Setup(c => c.CustomerFeedbacks).Returns(mockDbSet.Object);

            var result = await _service.CreateAsync(dto);

            Assert.Equal((int)System.Net.HttpStatusCode.Created, result.Status);
            mockDbSet.Verify(m => m.AddAsync(It.IsAny<Backend.Domain.Entities.CustomerFeedback>(), It.IsAny<CancellationToken>()), Times.Once());
            _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task ResolveAsync_FeedbackNotFound_ReturnsNotFound()
        {
            var dto = new ResolveCustomerFeedbackDto
            {
                ResolutionStatus = CustomerFeedbackStatus.Resolved
            };

            _contextMock.Setup(c => c.CustomerFeedbacks)
                .Returns(new List<Backend.Domain.Entities.CustomerFeedback>().AsQueryable().BuildMockDbSet().Object);

            var result = await _service.ResolveAsync(99, dto);

            Assert.Equal((int)System.Net.HttpStatusCode.NotFound, result.Status);
        }

        [Fact]
        public async Task ResolveAsync_Success_UpdatesStatus()
        {
            var dto = new ResolveCustomerFeedbackDto
            {
                ResolutionStatus = CustomerFeedbackStatus.Resolved,
                ResolutionNote = "Đã đổi hàng"
            };

            var feedback = new Backend.Domain.Entities.CustomerFeedback
            {
                Id = 1,
                ResolutionStatus = CustomerFeedbackStatus.Open
            };

            _contextMock.Setup(c => c.CustomerFeedbacks)
                .Returns(new List<Backend.Domain.Entities.CustomerFeedback> { feedback }.AsQueryable().BuildMockDbSet().Object);

            var result = await _service.ResolveAsync(1, dto);

            Assert.Equal((int)System.Net.HttpStatusCode.OK, result.Status);
            Assert.Equal(CustomerFeedbackStatus.Resolved, feedback.ResolutionStatus);
            Assert.Equal("Đã đổi hàng", feedback.ResolutionNote);
            Assert.NotNull(feedback.ResolvedAt);
            _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task GetTraceInvestigationAsync_NotQualityType_ReturnsBadRequest()
        {
            var feedback = new Backend.Domain.Entities.CustomerFeedback
            {
                Id = 1,
                FeedbackType = CustomerFeedbackType.Other
            };

            _contextMock.Setup(c => c.CustomerFeedbacks)
                .Returns(new List<Backend.Domain.Entities.CustomerFeedback> { feedback }.AsQueryable().BuildMockDbSet().Object);

            var result = await _service.GetTraceInvestigationAsync(1);

            Assert.Equal((int)System.Net.HttpStatusCode.BadRequest, result.Status);
            Assert.Contains("Chỉ hỗ trợ truy vết cho khiếu nại về Chất lượng", result.Message);
        }

        [Fact]
        public async Task GetTraceInvestigationAsync_HasAllocationId_CallsTraceabilityService()
        {
            var feedback = new Backend.Domain.Entities.CustomerFeedback
            {
                Id = 1,
                FeedbackType = CustomerFeedbackType.Quality,
                PaddyLotBagAllocationId = 100
            };

            var allocation = new PaddyLotBagAllocation
            {
                Id = 100,
                Bag = new PaddyLotBag { LotId = 500 }
            };

            _contextMock.Setup(c => c.CustomerFeedbacks)
                .Returns(new List<Backend.Domain.Entities.CustomerFeedback> { feedback }.AsQueryable().BuildMockDbSet().Object);
            _contextMock.Setup(c => c.PaddyLotBagAllocations)
                .Returns(new List<PaddyLotBagAllocation> { allocation }.AsQueryable().BuildMockDbSet().Object);
            
            var expectedTrace = ApiResponse.Success(new { LoteCode = "LOT-1" });
            _traceabilityServiceMock.Setup(x => x.GetByLotIdAsync(500, true, true, true, true, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedTrace);

            var result = await _service.GetTraceInvestigationAsync(1);

            Assert.Equal((int)System.Net.HttpStatusCode.OK, result.Status);
            _traceabilityServiceMock.Verify(x => x.GetByLotIdAsync(500, true, true, true, true, 10, It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
