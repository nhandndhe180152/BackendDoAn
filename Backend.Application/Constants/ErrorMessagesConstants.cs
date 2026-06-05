using System;

namespace Backend.Application.Constants;

public static class ErrorMessagesConstants
{
    private static readonly Dictionary<string, string> _messages = new()
    {
        // Auth
        [ApiCodeConstants.Auth.UserNotFound] = "Tên đăng nhập hoặc mật khẩu không đúng.",
        [ApiCodeConstants.Auth.EmailNotFound] = "Email không tồn tại.",
        [ApiCodeConstants.Auth.UserLocked] = "Tài khoản cuả bạn đã bị khoá, vui lòng đợi đến {ExpireTime} để tiếp tục sử dụng.",
        [ApiCodeConstants.Auth.UserNotActivated] = "Tài khoản chưa được kích hoạt.",
        [ApiCodeConstants.Auth.UserDeactivated] = "Tài khoản đã bị vô hiệu hoá, vui lòng liên hệ quản trị viên.",
        [ApiCodeConstants.Auth.RequiredAdminUser] = "Tài khoản của bạn chưa phải quản trị viên.",
        [ApiCodeConstants.Auth.InvalidToken] = "Access token không hợp lệ.",
        [ApiCodeConstants.Auth.InvalidRefreshToken] = "Refresh token không hợp lệ.",
        [ApiCodeConstants.Auth.RefreshTokenExpired] = "Refresh token đã hết hạn.",
        [ApiCodeConstants.Auth.RefreshTokenIsUsed] = "Refresh token đã được sử dụng.",
        [ApiCodeConstants.Auth.AccessTokenRevoked] = "Access token đã bị thu hồi.",
        [ApiCodeConstants.Auth.AccessTokenNotExpired] = "Access token chưa hết hạn.",
        [ApiCodeConstants.Auth.VerificationCodeHasExpired] = "Mã xác thực đã hết hạn.",
        [ApiCodeConstants.Auth.ForgotPasswordReachToLimit] = $"Quá số lượt yêu cầu trong ngày (tối đa {AuthConstants.MAX_ACCESS_FAILED} lần), vui lòng liên hệ quản trị viên.",
        [ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword] = $"Mật khẩu xác nhận phải trùng với mật khẩu mới.",
        [ApiCodeConstants.Auth.WrongOldPassword] = "Mật khẩu cũ không đúng.",
        [ApiCodeConstants.Auth.InvalidNewPassword] = "Mật khẩu mới phải bảo gồm ít nhất một chữ hoa, thường, số và ký tự đặc biệt.",
        [ApiCodeConstants.Auth.VerificationCodeUsed] = "Mã xác thực đã được sử dụng.",


        // Social Login Messages
        [ApiCodeConstants.Auth.SocialAccountNotExists] = "Tài khoản chưa tồn tại. Vui lòng đăng ký trước khi đăng nhập.",
        [ApiCodeConstants.Auth.GoogleEmailMismatch] = "Email không khớp với tài khoản Google.",
        [ApiCodeConstants.Auth.AppleEmailRequired] = "Lần đăng nhập Apple đầu tiên cần có thông tin email.",

        // Common
        [ApiCodeConstants.Common.Forbidden] = "Bạn không có quyền truy cập, vui lòng liên hệ quản trị viên.",
        [ApiCodeConstants.Common.Unauthorized] = "Phiên đăng nhập của bạn đã hết hạn, vui lòng đăng nhập lại.",
        [ApiCodeConstants.Common.InternalServerError] = "Đã có lỗi xảy ra, vui lòng liên hệ quản trị viên.",
        [ApiCodeConstants.Common.UnprocessableEntity] = "Dữ liệu gửi lên không hợp lệ.",
        [ApiCodeConstants.Common.NotFound] = "Không tìm thấy dữ liệu phù hợp.",
        [ApiCodeConstants.Common.DuplicatedData] = "Bản ghi {key} đã tồn tại.",
        [ApiCodeConstants.Common.BadRequest] = "Yêu cầu không hợp lê. Vui lòng kiểm tra lại định dạng dữ liệu.",
        [ApiCodeConstants.Common.RequiredMessage] = "{PropertyName} không được để trống.",
        [ApiCodeConstants.Common.InvalidFormatMessage] = "{PropertyName} không đúng định dạng.",
        [ApiCodeConstants.Common.MaxLengthMessage] = "{PropertyName} không được vượt quá {MaxLength} ký tự.",
        [ApiCodeConstants.Common.MinLengthMessage] = "{PropertyName} phải có ít nhất {MinLength} ký tự.",
        [ApiCodeConstants.Common.InvalidData] = "{PropertyName} dữ liệu không hợp lệ.",
        [ApiCodeConstants.Common.LessThanMessage] = "{PropertyName} phải nhỏ hơn {ComparisonProperty}.",
        [ApiCodeConstants.Common.GreaterThanMessage] = "{PropertyName} phải lớn hơn {ComparisonProperty}.",
        [ApiCodeConstants.Common.LessThanValueMessage] = "{PropertyName} phải nhỏ hơn {ComparisonValue}.",
        [ApiCodeConstants.Common.GreaterThanValueMessage] = "{PropertyName} phải lớn hơn {ComparisonValue}.",
        [ApiCodeConstants.Common.LessThanOrEqualMessage] = "{PropertyName} phải nhỏ hơn hoặc bằng {ComparisonProperty}.",
        [ApiCodeConstants.Common.GreaterThanOrEqualMessage] = "{PropertyName} phải lớn hơn hoặc bằng {ComparisonProperty}.",
        [ApiCodeConstants.Common.LessThanValueOrEqualMessage] = "{PropertyName} phải nhỏ hơn hoặc bằng {ComparisonValue}.",
        [ApiCodeConstants.Common.GreaterThanValueOrEqualMessage] = "{PropertyName} phải lớn hơn hoặc bằng {ComparisonValue}.",
        [ApiCodeConstants.Common.TooManyRequests] = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.",
        [ApiCodeConstants.Common.DuplicatedValueMessage] = "{PropertyName} bị trùng.",

        //File manager
        [ApiCodeConstants.FileManager.ReachToLimitTotalFile] = "Chỉ được phép tải lên tối đa {max} tệp tin.",
        [ApiCodeConstants.FileManager.ReachToLimitTotalSize] = "Tổng số dung lượng tải lên không được vượt quá {max} MB.",
        [ApiCodeConstants.FileManager.ReachToLimitImageSize] = "Vui lòng chọn những hình ảnh có kích thuóc nhỏ hơn {max} MB.",
        [ApiCodeConstants.FileManager.InvalidFile] = "Chỉ cho phép tải lên các tệp hình ảnh, âm thanh, video và tài liệu.",
        [ApiCodeConstants.FileManager.InvalidFolderName] = "Tên thư mục không hợp lệ.",
        [ApiCodeConstants.FileManager.FolderNotFound] = "Thư mục không tồn tại hoặc đã bị xoá.",
        [ApiCodeConstants.FileManager.ParentFolderNotFound] = "Thư mục cha không tồn tại hoặc đã bị xoá.",

        //User
        [ApiCodeConstants.User.DuplicatedEmail] = "Email {key} đã tồn tại.",
        [ApiCodeConstants.User.DuplicatedPhoneNumber] = "Số điện thoại {key} đã tồn tại.",
        [ApiCodeConstants.User.DuplicatedIdentityNumber] = "CCCD {key} đã tồn tại.",
    };

    public static string GetMessage(string code)
    {
        return _messages.TryGetValue(code, out var message)
            ? message
            : "Đã xảy ra lỗi không xác định.";
    }

}
