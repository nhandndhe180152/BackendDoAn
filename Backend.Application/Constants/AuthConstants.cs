using System;

namespace Backend.Application.Constants;

public static class AuthConstants
{
    public const int MAX_ACCESS_FAILED = 5;
    public const int EXPIRE_TIME_LOCKED = 1;
    public const int FORGOT_PASSWORD_TOKEN_EXPIRE_HOURS = 1;
    public const string FORGOT_PASSWORD_EMAIL_TITLE = "Đặt lại mật khẩu";

    public const int ACCOUNT_ACTIVATION_EXPIRE_TIME = 24;
    public const string ACCOUNT_ACTIVATION_EMAIL_TITLE = "Kích hoạt tài khoản";

    public static class EmailTemplates
    {
        public const string ADMIN_FORGOT_PASSWORD = "AdminForgotPassword";
        public const string CLIENT_FORGOT_PASSWORD = "ClientForgotPassword";
        public const string CLIENT_FORGOT_PASSWORD_OTP = "ClientForgotPassword_OTP";
        public const string ADMIN_ACCOUNT_ACTIVATION = "Register";
    }
}
