using System;
using System.Collections.Generic;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class ProductDTParameters : DTParameter
{
    public List<int> CategoryIds { get; set; } = new List<int>();
}
