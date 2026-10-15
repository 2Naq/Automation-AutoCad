using System;
using System.Reflection;
using System.Text;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Helper đăng ký các bộ mã hóa mở rộng (CodePage 1252, Windows-1252, ISO-8859-1).
    /// Đảm bảo tính tương thích tuyệt đối cho cả:
    /// - AutoCAD 2021, 2022, 2023, 2024 (chạy trên .NET Framework 4.8)
    /// - AutoCAD 2025, 2026+ (chạy trên .NET 8 / CoreCLR)
    /// Khi các thư viện như PdfSharp đọc/ghi file PDF và truy xuất CodePage 1252.
    /// </summary>
    public static class EncodingHelper
    {
        private static bool _isRegistered = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Đảm bảo bộ mã hóa CodePage 1252 (Windows-1252) được đăng ký thành công vào EncodingProvider.
        /// An toàn để gọi nhiều lần từ bất kỳ thread nào.
        /// </summary>
        public static void EnsureCodePagesRegistered()
        {
            if (_isRegistered) return;

            lock (_lock)
            {
                if (_isRegistered) return;

                // 1. Kiểm tra xem CodePage 1252 đã hoạt động bình thường chưa
                try
                {
                    Encoding testEnc = Encoding.GetEncoding(1252);
                    if (testEnc != null)
                    {
                        _isRegistered = true;
                        return;
                    }
                }
                catch
                {
                    // Ngoại lệ NotSupportedException xảy ra trên .NET 8 / CoreCLR (AutoCAD 2025-2026)
                    // Tiến hành đăng ký provider bên dưới
                }

                // 2. Thử lấy và đăng ký CodePagesEncodingProvider chính thức có sẵn trong runtime .NET 8
                try
                {
                    Type? providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages");

                    if (providerType == null)
                    {
                        try
                        {
                            Assembly asm = Assembly.Load("System.Text.Encoding.CodePages");
                            if (asm != null)
                            {
                                providerType = asm.GetType("System.Text.CodePagesEncodingProvider");
                            }
                        }
                        catch { }
                    }

                    if (providerType == null)
                    {
                        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            try
                            {
                                providerType = asm.GetType("System.Text.CodePagesEncodingProvider");
                                if (providerType != null) break;
                            }
                            catch { }
                        }
                    }

                    if (providerType != null)
                    {
                        PropertyInfo? prop = providerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (prop != null)
                        {
                            EncodingProvider? provider = prop.GetValue(null) as EncodingProvider;
                            if (provider != null)
                            {
                                Encoding.RegisterProvider(provider);
                            }
                        }
                    }
                }
                catch
                {
                    // Bỏ qua lỗi reflection để tiếp tục fallback
                }

                // 3. Kiểm tra lại; nếu vẫn chưa có, đăng ký Fallback1252EncodingProvider nội bộ
                try
                {
                    Encoding test = Encoding.GetEncoding(1252);
                    if (test != null)
                    {
                        _isRegistered = true;
                        return;
                    }
                }
                catch
                {
                    // Vẫn chưa được, dùng Fallback provider tự định nghĩa
                }

                try
                {
                    Encoding.RegisterProvider(new Fallback1252EncodingProvider());
                    _isRegistered = true;
                }
                catch
                {
                    // Fallback cuối cùng
                }
            }
        }

        /// <summary>
        /// Bộ cung cấp mã hóa dự phòng cho CodePage 1252 (Windows-1252 / ISO-8859-1)
        /// Phục vụ cho PdfSharp khi đọc / xuất PDF trên môi trường .NET Core / .NET 8
        /// </summary>
        private class Fallback1252EncodingProvider : EncodingProvider
        {
            public override Encoding? GetEncoding(int codepage)
            {
                if (codepage == 1252)
                {
                    try
                    {
                        return Encoding.GetEncoding("ISO-8859-1");
                    }
                    catch
                    {
                        return Encoding.Default;
                    }
                }
                return null;
            }

            public override Encoding? GetEncoding(string name)
            {
                if (string.Equals(name, "windows-1252", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "cp1252", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "ansi_x3.4-1968", StringComparison.OrdinalIgnoreCase))
                {
                    return GetEncoding(1252);
                }
                return null;
            }
        }
    }
}
