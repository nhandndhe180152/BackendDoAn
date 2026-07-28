using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class PartyDebtDTParameters : DTParameter
{
    /// <summary>PAYABLE | RECEIVABLE</summary>
    public string? Direction { get; set; }

    /// <summary>Chỉ lấy các sổ đang có số tiền quá hạn.</summary>
    public bool OverdueOnly { get; set; }
}

public class DebtDocumentDTParameters : DTParameter
{
    /// <summary>PAYABLE | RECEIVABLE</summary>
    public string? Direction { get; set; }
    public bool OverdueOnly { get; set; }
    public string? Status { get; set; }
}

public class DebtTransactionDTParameters : DTParameter
{
    public string? Direction { get; set; }
    public string? TransactionType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
