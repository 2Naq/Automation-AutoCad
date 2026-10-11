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
    }
}
