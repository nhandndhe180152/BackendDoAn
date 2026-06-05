using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class InventoryTransactionDTParameters : DTParameter
{
    public int? WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? ProductVariantId { get; set; }

    public string? TransactionType { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
