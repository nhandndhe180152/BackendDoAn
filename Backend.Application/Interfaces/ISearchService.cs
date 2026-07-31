using System.Threading.Tasks;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISearchService
{
    /// <summary>
    /// Tìm kiếm toàn cục theo từ khóa cho thanh tìm kiếm trên header.
    /// Trả về các nhóm kết quả (sản phẩm, biến thể, lô lúa/gạo, đơn bán, đơn nhập, khách hàng, nông dân, NCC).
    /// </summary>
    Task<ApiResponse> GlobalSearchAsync(string? keyword, int limitPerGroup = 5);
}
