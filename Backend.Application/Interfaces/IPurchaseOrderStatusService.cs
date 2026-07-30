using System;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPurchaseOrderStatusService : IServiceBase<int, CreatePurchaseOrderStatusDto, UpdatePurchaseOrderStatusDto, DTParameter>
{
}
