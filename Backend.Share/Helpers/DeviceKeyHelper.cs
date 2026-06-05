using System;

namespace Backend.Share.Helpers;

public class DeviceKeyHelper
{
    /// <summary>
    /// Mã hóa một chuỗi khóa (key) thành một chuỗi hash bảo mật bằng thuật toán BCrypt.
    /// </summary>
    /// <param name="key">Chuỗi ký tự gốc cần mã hóa (ví dụ: API key, mật khẩu thiết bị).</param>
    /// <returns>Chuỗi hash đã được mã hóa, sẵn sàng để lưu vào cơ sở dữ liệu.</returns>
    /// <remarks>
    /// **Cách hoạt động và Logic của BCrypt.HashPassword:**
    /// 1. **Tự động tạo Salt ngẫu nhiên:** Hàm này tự động tạo ra một chuỗi ngẫu nhiên gọi là Salt. Chuỗi Salt này giúp chống lại các cuộc tấn công bằng bảng tra cứu sẵn (Rainbow Table).
    /// 2. **Cơ chế tiệm tiến (Work Factor):** BCrypt sử dụng một tham số gọi là "Work Factor" (mặc định thường là 10 hoặc 11). Tham số này quyết định số vòng lặp mã hóa ($2^{work\_factor}$). Số vòng lặp càng cao, thời gian tính toán càng lâu, giúp chống lại tấn công dò mật khẩu (Brute-force).
    /// 3. **Cấu trúc chuỗi kết quả:** Chuỗi trả về có dạng đặc trưng (ví dụ: $2a$11$...). Trong đó bao gồm: Phiên bản BCrypt, Work Factor, Chuỗi Salt, và đoạn Hash thực tế được gộp chung làm một. Vì vậy, bạn không cần phải lưu riêng chuỗi Salt.
    /// </remarks>
    public static string HashKey(string key)
    {
        // Thực hiện băm chuỗi key gốc kết hợp với salt ngẫu nhiên
        return BCrypt.Net.BCrypt.HashPassword(key);
    }

    /// <summary>
    /// Xác thực chuỗi nhập vào (input) có khớp với chuỗi hash đã lưu trong cơ sở dữ liệu hay không.
    /// </summary>
    /// <param name="input">Chuỗi văn bản thuần túy do người dùng hoặc thiết bị gửi lên để kiểm tra.</param>
    /// <param name="key">Chuỗi hash bảo mật đã được lưu từ trước (kết quả của hàm HashKey).</param>
    /// <returns>True nếu trùng khớp hoàn toàn; ngược lại trả về False.</returns>
    /// <remarks>
    /// **Cách hoạt động và Logic của BCrypt.Verify:**
    /// 1. **Tách thông tin từ chuỗi Hash:** Vì chuỗi `key` (chuỗi hash đã lưu) chứa sẵn thông tin về Salt và Work Factor bên trong nó, BCrypt sẽ tự động trích xuất các thông tin này ra.
    /// 2. **Tái băm chuỗi Input:** BCrypt lấy chuỗi `input` do người dùng truyền vào, kết hợp với chuỗi Salt vừa trích xuất được, rồi thực hiện lại thuật toán băm với số vòng lặp (Work Factor) tương ứng.
    /// 3. **So sánh an toàn:** Sau khi băm xong chuỗi `input`, BCrypt sẽ so sánh kết quả này với đoạn mã hash thực tế nằm trong chuỗi `key`. Quá trình so sánh này được tối ưu hóa để chống tấn công đo lường thời gian (Timing Attack). Nếu khớp, hàm trả về true.
    /// </remarks>
    public static bool VerifyKey(string input, string key)
    {
        // Tiến hành băm lại chuỗi input với Salt lấy từ chuỗi key và so sánh kết quả
        return BCrypt.Net.BCrypt.Verify(input, key);
    }
}
