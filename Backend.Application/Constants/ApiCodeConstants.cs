using System;

namespace Backend.Application.Constants;

public static class ApiCodeConstants
{
    public static class Auth
    {
        public const string UserNotFound = "AUTH_404_01";
        public const string EmailNotFound = "AUTH_404_02";
        public const string UserLocked = "AUTH_403_01";
        public const string UserNotActivated = "AUTH_403_02";
        public const string UserDeactivated = "AUTH_403_03";
        public const string RequiredAdminUser = "AUTH_403_04";
        public const string InvalidToken = "AUTH_401_01";
        public const string InvalidRefreshToken = "AUTH_422_01";
        public const string RefreshTokenExpired = "AUTH_401_02";
        public const string RefreshTokenIsUsed = "AUTH_401_03";
        public const string AccessTokenRevoked = "AUTH_401_04";
        public const string AccessTokenNotExpired = "AUTH_422_02";
        public const string VerificationCodeHasExpired = "AUTH_422_03";
        public const string ForgotPasswordReachToLimit = "AUTH_401_05";
        public const string ConfirmPasswordNotMatchPassword = "AUTH_422_04";
        public const string SocialAccountNotExists = "AUTH_404_03";
        public const string GoogleEmailMismatch = "AUTH_400_01";
        public const string AppleEmailRequired = "AUTH_422_05";
        public const string WrongOldPassword = "AUTH_422_06";
        public const string InvalidNewPassword = "AUTH_422_07";
        public const string VerificationCodeUsed = "AUTH_422_08";
    }

    public static class Common
    {
        public const string Sucess = "CMN_200";
        public const string Created = "CMN_201";
        public const string NoContent = "CMN_204";
        public const string BadRequest = "CMN_400";
        public const string Unauthorized = "CMN_401";
        public const string Forbidden = "CMN_403";
        public const string NotFound = "CMN_404";
        public const string DuplicatedData = "CMN_409";
        public const string UnprocessableEntity = "CMN_422";
        public const string RequiredMessage = "CMN_422_01";
        public const string InvalidFormatMessage = "CMN_422_02";
        public const string MaxLengthMessage = "CMN_422_03";
        public const string MinLengthMessage = "CMN_422_04";
        public const string InvalidData = "CMN_422_05";
        public const string LessThanMessage = "CMN_422_06";
        public const string GreaterThanMessage = "CMN_422_07";
        public const string LessThanValueMessage = "CMN_422_08";
        public const string GreaterThanValueMessage = "CMN_422_09";
        public const string LessThanOrEqualMessage = "CMN_422_10";
        public const string GreaterThanOrEqualMessage = "CMN_422_11";
        public const string LessThanValueOrEqualMessage = "CMN_422_12";
        public const string GreaterThanValueOrEqualMessage = "CMN_422_13";
        public const string DuplicatedValueMessage = "CMN_422_14";
        public const string TooManyRequests = "CMN_429_01";
        public const string InternalServerError = "CMN_500";
    }

    public static class FileManager
    {
        public const string ReachToLimitTotalFile = "FM_422_01";
        public const string ReachToLimitTotalSize = "FM_422_02";
        public const string ReachToLimitImageSize = "FM_422_03";
        public const string InvalidFile = "FM_422_04";
        public const string InvalidFolderName = "FM_422_05";
        public const string FolderNotFound = "FM_404_01";
        public const string ParentFolderNotFound = "FM_404_02";
    }

    public static class User
    {
        public const string DuplicatedEmail = "USR_422_01";
        public const string DuplicatedPhoneNumber = "USR_422_02";
        public const string DuplicatedIdentityNumber = "USR_422_03";
    }
}
