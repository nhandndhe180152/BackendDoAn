using System;
using Backend.Domain.Abstractions;
using Backend.Infrastructure.Persistence;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backend.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly BackendContext _context;

    public UnitOfWork(BackendContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync()
    {
        ApplyAuditDates();
        return await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Tự động cập nhật các field audit date cho entity.
    /// 
    /// Hàm này duyệt qua tất cả entity đang được EF Core theo dõi trong ChangeTracker.
    /// 
    /// Nếu entity đang ở trạng thái Added:
    /// - Đây là bản ghi mới.
    /// - Set CreatedDate = giờ Việt Nam hiện tại.
    /// - Set LastModifiedDate = giờ Việt Nam hiện tại.
    /// 
    /// Nếu entity đang ở trạng thái Modified:
    /// - Đây là bản ghi đang được cập nhật.
    /// - Không cho phép sửa CreatedDate.
    /// - Set LastModifiedDate = giờ Việt Nam hiện tại.
    /// </summary>
    private void ApplyAuditDates()
    {
        // Lấy thời gian hiện tại theo giờ Việt Nam.
        // Dùng helper để tránh lệch giờ do server/database đang chạy UTC.
        var now = DateTimeHelper.VietnamNow();

        // Duyệt qua toàn bộ entity đang được EF Core tracking.
        foreach (var entry in _context.ChangeTracker.Entries())
        {
            // Nếu entity null thì bỏ qua để tránh lỗi.
            if (entry.Entity == null)
                continue;

            // EntityState.Added nghĩa là entity mới được thêm vào DbContext.
            // Khi SaveChangesAsync chạy, EF Core sẽ insert bản ghi này vào database.
            if (entry.State == EntityState.Added)
            {
                // Set CreatedDate cho bản ghi mới.
                SetPropertyValue(entry, "CreatedDate", now);

                // Set LastModifiedDate cho bản ghi mới.
                // Khi mới tạo, LastModifiedDate có thể bằng CreatedDate.
                SetPropertyValue(entry, "LastModifiedDate", now);
            }

            // EntityState.Modified nghĩa là entity đã tồn tại trong database
            // và đang được cập nhật dữ liệu.
            if (entry.State == EntityState.Modified)
            {
                // Không cho phép update CreatedDate.
                // CreatedDate phải là ngày tạo ban đầu, không được thay đổi khi update.
                PreventPropertyModified(entry, "CreatedDate");

                // Set LastModifiedDate thành thời gian cập nhật mới nhất.
                SetPropertyValue(entry, "LastModifiedDate", now);
            }
        }
    }

    /// <summary>
    /// Set giá trị cho một property của entity nếu property đó tồn tại.
    /// 
    /// Hàm này dùng cách tìm property theo tên thay vì ép kiểu entity về BaseEntity.
    /// Ưu điểm:
    /// - Entity nào có CreatedDate hoặc LastModifiedDate thì tự set.
    /// - Entity nào không có property đó thì bỏ qua, không lỗi.
    /// - Không phụ thuộc tất cả entity có kế thừa chung một BaseEntity hay không.
    /// </summary>
    /// <param name="entry">EntityEntry đang được EF Core tracking</param>
    /// <param name="propertyName">Tên property cần set, ví dụ CreatedDate</param>
    /// <param name="value">Giá trị DateTime cần gán</param>
    private static void SetPropertyValue(EntityEntry entry, string propertyName, DateTime value)
    {
        // Tìm property trong entity theo tên.
        // Ví dụ propertyName = "CreatedDate".
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);

        // Nếu entity có property này thì set giá trị mới.
        // Nếu không có thì bỏ qua để tránh lỗi.
        if (property != null)
        {
            property.CurrentValue = value;
        }
    }

    /// <summary>
    /// Ngăn không cho một property bị update xuống database.
    /// 
    /// Dùng cho CreatedDate khi entity bị Modified.
    /// Vì CreatedDate chỉ nên được set một lần lúc tạo bản ghi,
    /// không nên bị thay đổi mỗi khi update entity.
    /// </summary>
    /// <param name="entry">EntityEntry đang được EF Core tracking</param>
    /// <param name="propertyName">Tên property cần chặn update</param>
    private static void PreventPropertyModified(EntityEntry entry, string propertyName)
    {
        // Tìm property trong entity theo tên.
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);

        // Nếu entity có property này thì đánh dấu IsModified = false.
        // EF Core sẽ không sinh câu SQL update cho property này.
        if (property != null)
        {
            property.IsModified = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
