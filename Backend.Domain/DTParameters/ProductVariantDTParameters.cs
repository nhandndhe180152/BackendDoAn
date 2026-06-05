using System;
using System.Collections.Generic;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class ProductVariantDTParameters : DTParameter
{
    public int? ProductId { get; set; }
}
