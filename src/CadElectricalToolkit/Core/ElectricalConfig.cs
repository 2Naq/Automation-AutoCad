using System;
using System.Collections.Generic;

namespace CadElectricalToolkit.Core
{
    /// <summary>
    /// NƠI QUẢN LÝ TẬP TRUNG TẤT CẢ CÁC TÊN ATTRIBUTE TAG VÀ TÊN BLOCK
    /// -------------------------------------------------------------
    /// Bạn có thể tự do thay đổi các giá trị ở đây để khớp 100% với
    /// thư viện Block sẵn có của công ty hoặc thói quen vẽ của bạn!
    /// </summary>
    public static class ElectricalConfig
    {
        #region 1. Cấu hình Rơ-le, Contactor, Timer, Khí cụ điện

        /// <summary>
        /// Tag tên thiết bị (Trong ảnh bản vẽ của bạn: "NAME")
        /// </summary>
        public static string TagDeviceName = "NAME";

        /// <summary>
        /// Tag chân tiếp điểm thứ nhất (Ví dụ: "TERM01")
        /// </summary>
        public static string TagPin1 = "TERM01";

        /// <summary>
        /// Tag chân tiếp điểm thứ hai (Ví dụ: "TERM02")
        /// </summary>
        public static string TagPin2 = "TERM02";

        /// <summary>
        /// Tag địa chỉ tham chiếu chéo cuộn coil trên block tiếp điểm (Trong bản vẽ của bạn: "ADDRESS_COIL")
        /// </summary>
        public static string TagAddressCrossRef = "ADDRESS_COIL";

        /// <summary>
        /// Danh sách các tên Tag dự phòng cho địa chỉ cuộn coil trên tiếp điểm
        /// </summary>
        public static string[] TagAddressContactAliases = new[]
        {
            "ADDRESS_COIL", "ADDRESS_C", "ADDRESS", "REF", "COIL_ADDR", "COIL_ADDRESS", "ADDRESSCOIL"
        };

        #endregion

        #region 2. Block KHUNG 14 CHÂN (Cross-reference Relay Frame)

        /// <summary>
        /// Tên Block khung thống kê 14 chân (Trong bản vẽ của bạn: "KHUNG 14 CHAN")
        /// </summary>
        public static string BlockRelayFrame = "KHUNG 14 CHAN";

        /// <summary>
        /// Tag tên thiết bị trên block khung 14 chân
        /// </summary>
        public static string TagRelayFrameName = "NAME";

        /// <summary>
        /// Tag model thiết bị trên block khung 14 chân
        /// </summary>
        public static string TagRelayFrameModel = "MODEL";

        /// <summary>
        /// Tag điện áp cuộn hút trên block khung 14 chân (ví dụ: "220VAC")
        /// </summary>
        public static string TagRelayFrameCoil = "COIL";

        /// <summary>
        /// Các Tag địa chỉ tham chiếu ADDRESS_1, ADDRESS_2, ADDRESS_3, ADDRESS_4
        /// trên block khung 14 chân (mỗi dòng tương ứng 1 cặp tiếp điểm).
        /// 
        /// Giá trị sẽ được ghi theo định dạng: "Trang-HàngCột" (ví dụ: "2-6B")
        ///   → Trang 2, Hàng 6, Cột B
        /// </summary>
        public static string[] TagRelayFrameAddresses = new[]
        {
            "ADDRESS_1", // Cặp tiếp điểm 1: chân 5-9 (NO) hoặc 1-9 (NC)
            "ADDRESS_2", // Cặp tiếp điểm 2: chân 6-10 (NO) hoặc 2-10 (NC)
            "ADDRESS_3", // Cặp tiếp điểm 3: chân 7-11 (NO) hoặc 3-11 (NC)
            "ADDRESS_4", // Cặp tiếp điểm 4: chân 8-12 (NO) hoặc 4-12 (NC)
        };

        /// <summary>
        /// Bản đồ ánh xạ: Cặp chân nào tương ứng với ADDRESS nào
        /// Key = cặp chân, Value = index trong TagRelayFrameAddresses
        /// 
        /// Relay 14 chân tiêu chuẩn (MY4, LY4):
        ///   Cặp 1: 5-9 (NO) / 1-9 (NC)   → ADDRESS_1 (Index 0)
        ///   Cặp 2: 6-10 (NO) / 2-10 (NC) → ADDRESS_2 (Index 1)
        ///   Cặp 3: 7-11 (NO) / 3-11 (NC) → ADDRESS_3 (Index 2)
        ///   Cặp 4: 8-12 (NO) / 4-12 (NC) → ADDRESS_4 (Index 3)
        /// </summary>
        public static Dictionary<string, int> PinPairToAddressIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // NO (Thường mở)
            { "5-9",   0 },
            { "9-5",   0 },
            { "6-10",  1 },
            { "10-6",  1 },
            { "7-11",  2 },
            { "11-7",  2 },
            { "8-12",  3 },
            { "12-8",  3 },

            // NC (Thường đóng)
            { "1-9",   0 },
            { "9-1",   0 },
            { "2-10",  1 },
            { "10-2",  1 },
            { "3-11",  2 },
            { "11-3",  2 },
            { "4-12",  3 },
            { "12-4",  3 },
        };

        /// <summary>
        /// Khoảng cách giữa các block KHUNG 14 CHÂN khi xếp ngang (đơn vị mm)
        /// </summary>
        public static double RelayFrameSpacing = 60.0;

        #endregion

        #region 3. Cấu hình Khung tên bản vẽ (Title Block / Frame-a4)

        /// <summary>
        /// Tên Block khung tên bản vẽ của bạn (Ví dụ: "Frame-a4")
        /// </summary>
        public static string BlockTitle = "Frame-a4";

        /// <summary>
        /// Tag số trang bản vẽ trên khung tên (Trong bản vẽ của bạn: "A00")
        /// </summary>
        public static string TagSheetNumber = "A00";

        /// <summary>
        /// Tag tổng số trang bản vẽ trên khung tên (Trong bản vẽ của bạn: "TSHEET")
        /// </summary>
        public static string TagTotalSheets = "TSHEET";

        /// <summary>
        /// Tag tên bản vẽ
        /// </summary>
        public static string TagSheetName = "DESCRIPTION-DRAWINGS";

        /// <summary>
        /// Tag lần sửa đổi
        /// </summary>
        public static string TagRevision = "REV";

        /// <summary>
        /// Tag ngày vẽ / sửa
        /// </summary>
        public static string TagDate = "DATE";

        /// <summary>
        /// Số lượng cột trong khung bản vẽ (A, B, C, D, E, F, G, H = 8 cột)
        /// </summary>
        public static int FrameColumns = 8;

        /// <summary>
        /// Số lượng hàng trong khung bản vẽ (1, 2, 3, 4, 5, 6 = 6 hàng)
        /// </summary>
        public static int FrameRows = 6;

        /// <summary>
        /// Ký hiệu cột (A=1, B=2, ... H=8). Mảng chỉ mục 0-based.
        /// </summary>
        public static string[] ColumnLetters = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };

        /// <summary>
        /// Tỉ lệ chiều cao khung tên (Title Block) ở đáy bản vẽ (so với tổng chiều cao khung).
        /// Trong khung A4/A3 tiêu chuẩn (như Frame-a4 VASCO), phần bảng tên công ty/dự án ở đáy chiếm ~14% chiều cao.
        /// Vùng lưới bản vẽ chứa các hàng 1-6 nằm ở phía trên phần khung tên này.
        /// Cài đặt này giúp loại bỏ sai lệch hàng (tránh việc hàng 4 bị tính thành hàng 3, hàng 5 bị thành hàng 4).
        /// Mặc định: 0.14 (14% chiều cao khung).
        /// </summary>
        public static double FrameBottomMarginRatio = 0.14;

        /// <summary>
        /// Tỉ lệ viền lề trên của khung bản vẽ (so với tổng chiều cao khung).
        /// Mặc định: 0.025 (2.5% chiều cao khung).
        /// </summary>
        public static double FrameTopMarginRatio = 0.025;

        /// <summary>
        /// Tỉ lệ viền lề trái của khung bản vẽ (lề đóng gáy, so với tổng chiều rộng khung).
        /// Mặc định: 0.035 (3.5% chiều rộng khung).
        /// </summary>
        public static double FrameLeftMarginRatio = 0.035;

        /// <summary>
        /// Tỉ lệ viền lề phải của khung bản vẽ (so với tổng chiều rộng khung).
        /// Mặc định: 0.02 (2.0% chiều rộng khung).
        /// </summary>
        public static double FrameRightMarginRatio = 0.02;

        #endregion

        #region 4. Cấu hình Gen số & Dây dẫn (Wire & Marker)

        public static string TagWireNumber = "WIRE_NO";
        public static string BlockWireNumber = "GEN_SO";

        #endregion

        #region 5. Cấu hình Cầu đấu dây (Terminal Strips)

        public static string TagTerminalStrip = "TB_TAG";
        public static string TagTerminalPin = "PIN_NO";
        public static string TagWireFrom = "WIRE_FROM";
        public static string TagWireTo = "WIRE_TO";
        public static string BlockTerminal = "CAU_DAU";

        #endregion

        #region 6. Cấu hình Module PLC I/O

        public static string TagPlcAddress = "PLC_ADDR";
        public static string TagPlcDescription = "DESC";

        #endregion
    }

    /// <summary>
    /// Thông tin thiết bị (Relay/Contactor/Timer) đã chọn trong lệnh TKCRL
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>Tên thiết bị (NAME tag value)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>ObjectId của block KHUNG 14 CHÂN đã tồn tại (nếu có)</summary>
        public Autodesk.AutoCAD.DatabaseServices.ObjectId ExistingFrameId { get; set; }

        /// <summary>ObjectId của block cuộn hút Coil (nếu tìm thấy)</summary>
        public Autodesk.AutoCAD.DatabaseServices.ObjectId CoilBlockId { get; set; }

        /// <summary>Vị trí đặt của cuộn hút Coil</summary>
        public Autodesk.AutoCAD.Geometry.Point3d CoilPosition { get; set; }

        /// <summary>Địa chỉ tọa độ của cuộn hút trên khung tên (ví dụ: "01-2B")</summary>
        public string CoilAddress { get; set; } = string.Empty;
    }
}
