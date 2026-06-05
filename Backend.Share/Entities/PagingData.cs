using System;

namespace Backend.Share.Entities;

public class PagingData<T>
{
    public List<T> DataSource { get; set; } = new List<T>();
    public int Total { get; set; }
    public int TotalFiltered { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalFiltered / (double)PageSize);
    public int PageSize { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
    public int FirstRowOnPage => Total > 0 ? (TotalFiltered > 0 ? (CurrentPage - 1) * PageSize + 1 : 0) : 0;
    public int LastRowOnPage => Math.Min(CurrentPage * PageSize, TotalFiltered);
    public string PageInfo => $"Đang xem {FirstRowOnPage} đến {LastRowOnPage} trong tổng số {TotalFiltered} mục " + (Total > TotalFiltered ? $"(được lọc từ {Total} mục)" : "");
}
