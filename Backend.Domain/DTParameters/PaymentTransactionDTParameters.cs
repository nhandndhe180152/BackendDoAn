using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class PaymentTransactionDTParameters : DTParameter
{


    public int? MyProvinceId { get; set; }
    public int? MyOfficeId { get; set; }
    public List<int> MyRoleIds { get; set; } = new List<int>();

    public List<int> UserIds { get; set; } = new List<int>();
    public List<int> OfficeIds { get; set; } = new List<int>();
    public List<int> PaymentStatusIds { get; set; } = new List<int>();

}
