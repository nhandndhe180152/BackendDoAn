using System.Threading.Tasks;
using Backend.Application.DTOs.Dashboard;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse> GetReportStatisticsAsync(string period);

    Task<ApiResponse> GetSummaryAsync(DashboardQuery query);
    Task<ApiResponse> GetTodayTasksAsync(DashboardQuery query);
    Task<ApiResponse> GetPurchaseChartAsync(DashboardQuery query);
    Task<ApiResponse> GetOperationalEfficiencyAsync(DashboardQuery query);
    Task<ApiResponse> GetRecentAlertsAsync(DashboardQuery query);
    Task<ApiResponse> GetInventoryByLotReportAsync(DashboardQuery query);
    Task<ApiResponse> GetInventoryByWarehouseReportAsync(DashboardQuery query);
    Task<ApiResponse> GetInventoryByProductVariantReportAsync(DashboardQuery query);
    Task<ApiResponse> GetTwoWayDebtReportAsync(DashboardQuery query);
    Task<ApiResponse> GetMillingYieldReportAsync(DashboardQuery query);
    Task<ApiResponse> GetSalesRevenueReportAsync(DashboardQuery query);
    Task<ApiResponse> GetQualityAlertsReportAsync(DashboardQuery query);
}
