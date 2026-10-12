using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadElectricalToolkit.UI.Services
{
    /// <summary>
    /// Kết quả khi chọn block mẫu trên CAD (dùng cho cả Chọn TAG và Chọn Block mẫu)
    /// </summary>
    public class PickBlockResult
    {
        /// <summary>Tên block đã chọn</summary>
        public string BlockName { get; set; } = "";

        /// <summary>Tag attribute gần nhất với điểm click chuột</summary>
        public string ClosestTag { get; set; } = "";

        /// <summary>Danh sách cặp (Tag, Value) của tất cả attributes trên block</summary>
        public Dictionary<string, string> AllAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Danh sách góc xoay (độ) của từng Tag attribute trên block</summary>
        public Dictionary<string, double> AttributeRotations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Góc xoay (độ) của Tag gần nhất hoặc Text đã click</summary>
        public double RotationDegrees { get; set; }

        /// <summary>Nội dung text nếu là Text/MText</summary>
        public string TextContent { get; set; } = "";

        /// <summary>True nếu đối tượng chọn là Text/MText (không phải Block)</summary>
        public bool IsText { get; set; }
    }

    /// <summary>
    /// Kết quả khi thực hiện đánh số hàng loạt
    /// </summary>
    public class NumberingResult
    {
        public int Count { get; set; }
        public int NextNumber { get; set; }
    }

    /// <summary>
    /// Kết quả khi thực hiện đổi attribute hàng loạt
    /// </summary>
    public class BatchEditResult
    {
        public int Count { get; set; }
    }

    /// <summary>
    /// Kết quả khi thực hiện xoay attribute / text
    /// </summary>
    public class RotateResult
    {
        public int Count { get; set; }
    }

    /// <summary>
    /// Interface tách biệt tương tác AutoCAD khỏi ViewModel.
    /// ViewModel gọi các phương thức này thay vì trực tiếp dùng Editor/Transaction.
    /// Điều này cho phép ViewModel được test độc lập và không phụ thuộc vào AutoCAD API.
    /// </summary>
    public interface ICadInteractionService
    {
        /// <summary>
        /// Kiểm tra AutoCAD có đang sẵn sàng hay không
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Chọn một block trên CAD để nhận diện tên Block và Tag gần nhất
        /// </summary>
        /// <param name="ownerWindow">Cửa sổ WPF sở hữu (dùng cho StartUserInteraction)</param>
        /// <param name="prompt">Thông báo hiển thị trên command line</param>
        /// <returns>Kết quả nhận diện, hoặc null nếu người dùng hủy</returns>
        PickBlockResult? PickBlock(System.Windows.Window ownerWindow, string prompt);

        /// <summary>
        /// Cho phép người dùng click trên CAD để lấy góc xoay của một Attribute / Text / Line
        /// </summary>
        double? PickAngle(System.Windows.Window ownerWindow);

        /// <summary>
        /// Lấy toàn bộ danh sách Attribute TAG của một block name từ database AutoCAD
        /// </summary>
        List<string> GetBlockAttributeTags(string blockName);

        /// <summary>
        /// Quét chọn và đánh số hàng loạt cho Block Attribute / Text
        /// </summary>
        NumberingResult NumberBlockAttributes(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum, int step,
            int styleIndex, bool twoDigits,
            string targetBlockName, string targetTag,
            int dirIndex, bool includeText);

        /// <summary>
        /// Đánh số từng đối tượng một (click từng cái)
        /// </summary>
        NumberingResult NumberOneByOne(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum, int step,
            int styleIndex, bool twoDigits, string targetTag);

        /// <summary>
        /// Quét chọn và đánh số khung tên bản vẽ
        /// </summary>
        NumberingResult NumberFrames(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum,
            int styleIndex, bool twoDigits,
            string targetBlockName, string targetAttrTag, int dirIndex,
            bool usePageMode, bool autoTotal, int totalPages,
            string totalPagesTag, string totalPrefix);

        /// <summary>
        /// Quét chọn và đổi attribute hàng loạt
        /// </summary>
        BatchEditResult BatchEditAttributes(
            System.Windows.Window ownerWindow,
            string targetBlockName, string targetTag,
            bool isDirect, string newValFixed,
            string findText, string replaceText, bool matchCase);

        /// <summary>
        /// Click từng block để đổi attribute
        /// </summary>
        BatchEditResult BatchEditOneByOne(
            System.Windows.Window ownerWindow,
            string targetTag,
            bool isDirect, string newValFixed,
            string findText, string replaceText, bool matchCase);

        /// <summary>
        /// Quét chọn và xoay attribute hàng loạt cho các Block được chọn
        /// </summary>
        RotateResult RotateAttributes(
            System.Windows.Window ownerWindow,
            string targetBlockName,
            string targetTag,
            double targetAngleDegrees,
            bool isRelative,
            bool includeText,
            bool keepCenter = true,
            double offsetDistance = 0.0);

        /// <summary>
        /// Click từng block trên CAD để xoay attribute
        /// </summary>
        RotateResult RotateOneByOne(
            System.Windows.Window ownerWindow,
            string targetTag,
            double targetAngleDegrees,
            bool isRelative,
            bool keepCenter = true,
            double offsetDistance = 0.0);

        /// <summary>
        /// Đồng bộ địa chỉ dây dẫn liên trang trên toàn bộ bản vẽ ModelSpace
        /// </summary>
        WireArrowSyncResult SyncWireArrowsAll(
            System.Windows.Window ownerWindow,
            WireArrowSyncOptions options);

        /// <summary>
        /// Đồng bộ địa chỉ dây dẫn liên trang theo vùng quét chọn trên CAD
        /// </summary>
        WireArrowSyncResult SyncWireArrowsSelected(
            System.Windows.Window ownerWindow,
            WireArrowSyncOptions options);

        /// <summary>
        /// Nối và gán địa chỉ trực tiếp giữa 2 mũi tên (Pick mũi tên A -> Pick mũi tên B)
        /// </summary>
        bool ConnectTwoArrowsManually(
            System.Windows.Window ownerWindow,
            WireArrowSyncOptions options);
    }

    /// <summary>
    /// Các tùy chọn đồng bộ địa chỉ dây dẫn / mũi tên To-From liên trang
    /// </summary>
    public class WireArrowSyncOptions
    {
        public string TargetBlockName { get; set; } = "Arrow - To - Block";
        public string WireTag { get; set; } = "WIRE_NAME";
        public string AddressToTag { get; set; } = "ADDRESS_TO";
        public string AddressFromTag { get; set; } = "ADDRESS_FROM";

        /// <summary>
        /// 0: Chỉ số trang (ví dụ "02")
        /// 1: Trang - Tọa độ ô lưới (ví dụ "02-5B")
        /// 2: Trang / Tổng trang (ví dụ "02/07")
        /// 3: Tùy biến (Prefix + Trang + Dấu phân cách + Ô lưới)
        /// </summary>
        public int FormatModeIndex { get; set; } = 0;

        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";
        public string GridSeparator { get; set; } = "-";
        public bool TwoDigits { get; set; } = true;
        public bool IncludeMultipleSheets { get; set; } = true;
        public bool UpdateBothWays { get; set; } = true;

        /// <summary>
        /// Khớp theo tên gốc trước dấu ngăn cách (ví dụ "DI.08-RB1" và "DI.08-RB2" cùng có gốc là "DI.08")
        /// </summary>
        public bool MatchBaseSignalOnly { get; set; } = false;
        public string BaseSignalSeparator { get; set; } = "-";
    }

    /// <summary>
    /// Kết quả đồng bộ địa chỉ dây liên trang
    /// </summary>
    public class WireArrowSyncResult
    {
        public int TotalArrowsFound { get; set; }
        public int TotalUpdated { get; set; }
        public int LinkedGroupCount { get; set; }
        public List<string> UnmatchedSignals { get; set; } = new();
        public List<string> Details { get; set; } = new();
    }
}
