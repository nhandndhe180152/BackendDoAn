using System;
using Backend.Share.Entities;

namespace Backend.Domain.Commons;

public class DTResultWithSummary<T, TSummary>
{
    public DTResult<T> Result { get; set; } = null!;
    public TSummary Summary { get; set; } = default!;
}
