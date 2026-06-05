using System;

namespace Backend.Application.Constants;

public static class NotificationConstants
{
    public static class TripRequestMessages
    {
        public const string Created_Title = "🆕 Yêu cầu điều xe mới cần duyệt!";
        //public const string Created_Body = "Người dùng {0} đã tạo yêu cầu chuyến đi từ {1} đến {2}.";
        public const string Created_Body = "Người dùng {0} đã tạo yêu cầu chuyến đi";

        public const string Approved_Title = "✅ Yêu cầu được phê duyệt";
        public const string Approved_Body_User = "Yêu cầu chuyến đi của bạn đã được phê duyệt. Chúng tôi đang sắp xếp tài xế.";
        public const string Approved_Body_Dispatcher = "Bạn đã phê duyệt yêu cầu chuyến đi từ {0} đến {1}.";

        public const string Rejected_Title = "❌ Yêu cầu điều xe của bạn đã bị từ chối";
        public const string Rejected_Body = "Rất tiếc, yêu cầu #{0} của bạn đã bị từ chối. Lý do: {1}.";

        public const string CancelledByUser_Title = "🔁 Yêu cầu đã duyệt bị hủy bởi người dùng";
        public const string CancelledByUser_Body = "Người yêu cầu {0} đã hủy yêu cầu chuyến đi.";

        public const string CancelledByDispatcher_Title = "🚫 Yêu cầu bị hủy bởi điều phối viên";
        public const string CancelledByDispatcher_Body = "Yêu cầu chuyến đi của bạn đã bị hủy bởi điều phối viên.";

        public const string PickupUpdated_Title = "✏️ Cập nhật điểm đón";
        public const string PickupUpdated_Body = "Người dùng {0} đã cập nhật điểm đón từ {1} → {2}. Vui lòng kiểm tra lại yêu cầu.";

        public const string DropoffUpdated_Title = "✏️ Cập nhật điểm đến";
        public const string DropoffUpdated_Body = "Người dùng {0} đã cập nhật điểm đến từ {1} → {2}. Vui lòng kiểm tra lại yêu cầu.";

        public const string DesiredTimeUpdated_Title = "⏰ Cập nhật thời gian mong muốn";
        public const string DesiredTimeUpdated_Body = "Người dùng {0} đã thay đổi thời gian khởi hành mong muốn từ {1} → {2}.";

        public const string RequestUpdated_Title = "🔄 Yêu cầu chuyến đi được cập nhật";
        public const string RequestUpdated_Body = "Người dùng {0} đã cập nhật yêu cầu chuyến đi. Vui lòng kiểm tra lại thông tin.";
    }

    public static class TripMessages
    {
        public const string Assigned_Title_Driver = "📌 Bạn được phân công chuyến đi";
        //public const string Assigned_Body_Driver = "Bạn được phân công chuyến đi #{0} từ {1} đến {2}, khởi hành lúc {3}.";
        public const string Assigned_Body_Driver = "Bạn được phân công chuyến đi #{0}, khởi hành lúc {1}.";


        public const string Assigned_Title_User = "🛻 Đang tìm tài xế cho bạn";
        public const string Assigned_Body_User = "Chúng tôi đã lên lịch chuyến đi và đang tìm tài xế phù hợp cho bạn.";

        public const string DriverAccepted_Title = "✅ Tài xế đã nhận chuyến đi";
        //public const string DriverAccepted_Body = "Tài xế {0} sẽ đón bạn tại {1} lúc {2}.";
        //public const string DriverAccepted_Body_Dispatcher = "Tài xế {0} đã nhận chuyến đi từ {1} đến {2}.";
        public const string DriverAccepted_Body = "Tài xế {0} sẽ đón bạn lúc {1} cho chuyến đi mã #{2}";
        public const string DriverAccepted_Body_Dispatcher = "Tài xế {0} đã nhận chuyến mã #{1}.";


        public const string DriverRejected_Title = "🚫 Tài xế từ chối chuyến đi";
        public const string DriverRejected_Body = "Tài xế {0} đã từ chối nhận chuyến đi được phân công.";

        public const string DriverMovingToPickup_Title = "🚗 Tài xế đang di chuyển đến điểm đón";
        //public const string DriverMovingToPickup_Body = "Tài xế {0} đang trên đường đến đón bạn tại {1}.";
        public const string DriverMovingToPickup_Body = "Tài xế {0} đang trên đường đến đón bạn, chuyến đi mã #{1} sẽ sớm khởi hành";


        //public const string DriverMovingToPickup_Body_Dispatcher = "Tài xế {0} đang di chuyển đến điểm đón tại {1}.";
        public const string DriverMovingToPickup_Body_Dispatcher = "Tài xế {0} đang di chuyển đến điểm đón của chuyến đi mã #{1}";


        public const string ArrivedAtPickupPoint_Title = "📍 Tài xế đã đến điểm đón";
        public const string ArrivedAtPickupPoint_Body = "Tài xế {0} đã đến điểm đón bạn. Vui lòng chuẩn bị lên xe.";

        public const string Started_Title = "🚗 Chuyến đi đã bắt đầu";
        public const string Started_Body = "Tài xế {0} đã bắt đầu chuyến đi từ {1} đến {2}.";

        //public const string ArrivedAtDropoffPoint_Title_User = "🛬 Bạn đã đến điểm đến";
        //public const string ArrivedAtDropoffPoint_Body_User = "Bạn đã đến nơi. Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi!";
        public const string ArrivedAtDropoffPoint_Title_User = "🛬 Bạn đã đến điểm đến tiếp theo trong chuyến";
        public const string ArrivedAtDropoffPoint_Body_User = "Bạn đã đến điểm đến {0}. Vui lòng xác nhận thông tin di chuyển với lái xe!";

        public const string ArrivedAtDropoffPoint_Title_Dispatcher = "🛬 Tài xế đã đến điểm đến";
        public const string ArrivedAtDropoffPoint_Body_Dispatcher = "Tài xế {0} đã đưa người dùng {1} đến điểm đến {2} lúc {3}.";

        public const string Completed_Title = "🎉 Chuyến đi đã hoàn tất";
        public const string Completed_Body_User = "Cảm ơn bạn đã sử dụng dịch vụ. Chuyến đi của bạn đã kết thúc lúc {0}.";
        //public const string Completed_Body_Dispatcher = "Tài xế {0} đã hoàn tất chuyến đi từ {1} đến {2}.";
        public const string Completed_Body_Dispatcher = "Tài xế {0} đã hoàn thành chuyến đi mã #{1}";


        public const string CancelledByUser_Title = "❌ Chuyến đi bị hủy bởi người dùng";
        //public const string CancelledByUser_Body = "Người dùng {0} đã hủy chuyến đi từ {1} đến {2}.";
        public const string CancelledByUser_Body = "Người dùng {0} đã hủy chuyến đi mã #{1}.";


        public const string CancelledByDispatcher_Title = "🚫 Chuyến đi bị hủy bởi điều phối viên";
        //public const string CancelledByDispatcher_Body = "Chuyến đi từ {0} đến {1} đã bị hủy bởi điều phối viên.";
        public const string CancelledByDispatcher_Body = "Chuyến đi mã #{0} đã bị hủy bởi điều phối viên.";


        public const string CancelledByDriver_Title = "🚫 Chuyến đi bị hủy bởi tài xế";
        public const string CancelledByDriver_Body = "Tài xế {0} đã hủy chuyến đi. Chúng tôi sẽ sắp xếp lại chuyến đi khác nếu cần.";
        public const string CancelledByDriver_Body_Dispatcher = "Tài xế {0} đã hủy chuyến đi. Vui lòng kiểm tra thông tin.";

        public const string DriverRemoved_Title = "🚫 Bạn đã bị gỡ khỏi chuyến đi";
        //public const string DriverRemoved_Body = "Bạn không còn phụ trách chuyến đi từ {0} đến {1}, khởi hành lúc {2}.";
        public const string DriverRemoved_Body = "Bạn không còn phụ trách chuyến đi #{0}, khởi hành lúc {1}.";

    }

    public static class FuelLogMessages
    {
        public const string Created_Title = "🆕 Lịch sử đổ nhiên liệu mới được thêm";
        public const string Created_Body = "Tài xế {0} đã thêm lịch sử đổ nhiên liệu. Vui lòng kiểm tra hệ thống.";

        public const string Updated_Title = "✏️ Lịch sử đổ nhiên liệu được cập nhật";
        public const string Updated_Body = "Tài xế {0} đã cập nhật lịch sử đổ nhiên liệu. Vui lòng kiểm tra lại thông tin.";

        public const string Approved_Title = "✅ Lịch sử đổ nhiên liệu được phê duyệt";
        public const string Approved_Body = "Lịch sử đổ nhiên liệu của bạn đã được điều phối viên phê duyệt.";

        public const string Rejected_Title = "❌ Lịch sử đổ nhiên liệu bị từ chối";
        public const string Rejected_Body = "Lịch sử đổ nhiên liệu của bạn đã bị điều phối viên từ chối. Lý do: {0}.";
    }

    public static class TripExpenseMessages
    {
        public const string Created_Title = "🆕 Chi phí chuyến đi mới được thêm";
        public const string Created_Body = "Tài xế {0} đã thêm chi phí chuyến đi. Vui lòng kiểm tra hệ thống.";

        public const string Updated_Title = "✏️ Chi phí chuyến đi được cập nhật";
        public const string Updated_Body = "Tài xế {0} đã cập nhật chi phí chuyến đi. Vui lòng kiểm tra lại thông tin.";

        public const string Approved_Title = "✅ Chi phí chuyến đi được phê duyệt";
        public const string Approved_Body = "Chi phí chuyến đi của bạn đã được điều phối viên phê duyệt.";

        public const string Rejected_Title = "❌ Chi phí chuyến đi bị từ chối";
        public const string Rejected_Body = "Chi phí chuyến đi của bạn đã bị điều phối viên từ chối. Lý do: {0}.";
    }

    public static class MaintenanceRecordMessages
    {
        public const string Created_Title = "🆕 Lịch sử sửa chữa - bảo dưỡng mới được thêm";
        public const string Created_Body = "Tài xế {0} đã thêm lịch sử sửa chữa - bảo dưỡng. Vui lòng kiểm tra hệ thống.";

        public const string Updated_Title = "✏️ Lịch sử sửa chữa - bảo dưỡng được cập nhật";
        public const string Updated_Body = "Tài xế {0} đã cập nhật lịch sử sửa chữa - bảo dưỡng. Vui lòng kiểm tra lại thông tin.";

        public const string Approved_Title = "✅ Lịch sử sửa chữa - bảo dưỡng được phê duyệt";
        public const string Approved_Body = "Lịch sử sửa chữa - bảo dưỡng của bạn đã được điều phối viên phê duyệt.";

        public const string Rejected_Title = "❌ Lịch sử sửa chữa - bảo dưỡng bị từ chối";
        public const string Rejected_Body = "Lịch sử sửa chữa - bảo dưỡng của bạn đã bị điều phối viên từ chối. Lý do: {0}.";
    }
}
