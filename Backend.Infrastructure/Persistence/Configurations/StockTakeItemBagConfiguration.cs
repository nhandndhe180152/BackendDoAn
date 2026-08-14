using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeItemBagConfiguration : IEntityTypeConfiguration<StockTakeItemBag>
{
    public void Configure(EntityTypeBuilder<StockTakeItemBag> builder)
    {
        builder.ToTable(TableNames.StockTakeItemBag);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.SystemWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.CountedWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.QrCode).HasMaxLength(100);
        builder.Property(x => x.QualityStatus).HasMaxLength(30).HasDefaultValue("OK");
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.Ignore(x => x.WeightDifference);
        builder.Ignore(x => x.IsMissing);

        builder.HasOne(x => x.StockTakeItem)
               .WithMany(x => x.Bags)
               .HasForeignKey(x => x.StockTakeItemId)
               .OnDelete(DeleteBehavior.Cascade);

        // Không cascade sang bao vật lý: xoá phiếu kiểm kê tuyệt đối không được
        // kéo theo bao hàng thật.
        builder.HasOne(x => x.PaddyLotBag)
               .WithMany()
               .HasForeignKey(x => x.PaddyLotBagId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CountedByUser)
               .WithMany()
               .HasForeignKey(x => x.CountedByUserId)
               .OnDelete(DeleteBehavior.NoAction)
               .IsRequired(false);

        // Một bao chỉ xuất hiện MỘT lần trong một dòng kiểm kê — chặn quét trùng
        // ở tầng DB, không chỉ dựa vào kiểm tra trong service.
        builder.HasIndex(x => new { x.StockTakeItemId, x.PaddyLotBagId }).IsUnique();
    }
}
