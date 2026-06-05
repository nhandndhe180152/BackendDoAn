using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class TagDTParamters : DTParameter
{
    public bool IsIncludeTagType { get; set; } = true;
    public List<int> TagTypeIds { get; set; } = new List<int>();

}
