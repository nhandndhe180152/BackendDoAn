using System;
using System.Text.RegularExpressions;

namespace Backend.Share.Helpers;

public static class PhoneHelper
    {
        // Số điện thoại Việt Nam hợp lệ: di động 10 số (bắt đầu từ 03, 05, 07, 08, 09) hoặc số cố định 11 số (bắt đầu từ 02)
        private static readonly Regex VietnamPhoneRegex = new Regex(
            @"^(0[35789][0-9]{8}|02[0-9]{9})$",
            RegexOptions.Compiled
        );

        public static bool IsValidVietnamPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            return VietnamPhoneRegex.IsMatch(phone);
        }
    }
