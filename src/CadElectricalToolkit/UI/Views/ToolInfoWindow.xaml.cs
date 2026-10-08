using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Model dữ liệu cho mỗi lệnh trong danh sách
    /// </summary>
    public class CommandItem
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
    }

    /// <summary>
    /// Giao diện WPF hiện đại hiển thị danh sách lệnh và thông tin Plugin
    /// </summary>
    public partial class ToolInfoWindow : Window
    {
        private readonly List<CommandItem> _allCommands;

        public ToolInfoWindow()
        {
            InitializeComponent();

            _allCommands = new List<CommandItem>
            {
                new CommandItem
                {
                    Code = "DSTT",
                    Title = "Đánh số thứ tự tăng dần",
                    Description = "Giao diện WPF: Đánh số Block Attribute, Text/MText, tuỳ chỉnh Prefix, Suffix, From, bước nhảy và sắp xếp hướng tự động.",
                    Category = "Đánh số & Soạn thảo"
                },
                new CommandItem
                {
                    Code = "DSTTKBT",
                    Title = "Đánh số thứ tự khung tên bản vẽ",
                    Description = "Giao diện WPF: Quản lý số trang, định dạng trang hiện tại/tổng trang (A00='01', TSHEET='/24'), tự động đếm tổng số trang.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "DOIATT",
                    Title = "Đổi giá trị Attribute hàng loạt",
                    Description = "Giao diện WPF: Chọn block mẫu trên CAD để tự nạp danh sách TAG (ví dụ: TERM01, TERM02, MODEL...), gán giá trị mới hoặc Tìm & Thay thế hàng loạt.",
                    Category = "Đánh số & Soạn thảo"
                },
                new CommandItem
                {
                    Code = "CDN",
                    Title = "Cộng Dimension nhanh",
                    Description = "Tính tổng chiều dài tất cả các kích thước Dimension được chọn và tự sao chép kết quả vào Clipboard.",
                    Category = "Đánh số & Soạn thảo"
                },
                new CommandItem
                {
                    Code = "GTD",
                    Title = "Gom Text dọc (Gióng hàng thẳng đứng)",
                    Description = "Căn chỉnh các đối tượng Text/MText thẳng hàng theo phương dọc với khoảng cách đều nhau.",
                    Category = "Đánh số & Soạn thảo"
                },
                new CommandItem
                {
                    Code = "GTN",
                    Title = "Gom Text ngang (Gióng hàng nằm ngang)",
                    Description = "Căn chỉnh các đối tượng Text/MText thẳng hàng theo phương ngang với khoảng cách đều nhau.",
                    Category = "Đánh số & Soạn thảo"
                },
                new CommandItem
                {
                    Code = "TCCTB",
                    Title = "Đánh địa chỉ cuộn coil cho tiếp điểm",
                    Description = "Tự động truy xuất vị trí cuộn coil (SheetNo-RowCol) và ghi vào thuộc tính ADDRESS_COIL của các tiếp điểm NO/NC.",
                    Category = "Relay & Khung 14 chân"
                },
                new CommandItem
                {
                    Code = "TKCRL",
                    Title = "Thống kê chân số Relay / Contactor",
                    Description = "Tạo bảng tham chiếu KHUNG 14 CHAN, tự động tính tọa độ các cặp chân tiếp điểm và cho phép đặt vị trí trực quan.",
                    Category = "Relay & Khung 14 chân"
                },
                new CommandItem
                {
                    Code = "SYNCRL",
                    Title = "Đồng bộ tiếp điểm & cuộn coil 2 chiều",
                    Description = "Cập nhật đồng thời: Coil -> ADDRESS_COIL trên tiếp điểm, và Tiếp điểm -> ADDRESS_1..4 trên Khung 14 chân.",
                    Category = "Relay & Khung 14 chân"
                },
                new CommandItem
                {
                    Code = "KTCRL",
                    Title = "Kiểm tra chân số thiết bị",
                    Description = "Phát hiện tiếp điểm mồ côi (không có coil), kiểm tra trùng lặp cặp chân hoặc vượt quá số lượng tiếp điểm cho phép.",
                    Category = "Relay & Khung 14 chân"
                },
                new CommandItem
                {
                    Code = "TIMTRANG",
                    Title = "Tìm trang & vẽ đường chỉ dẫn",
                    Description = "Click vào tiếp điểm / mũi tên để tìm trang đích và vẽ đường dóng trực tiếp đến khung tên trang đó.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "XOALINK",
                    Title = "Xóa đường chỉ dẫn truy vết trang",
                    Description = "Dọn dẹp nhanh tất cả các đường mũi tên/line chỉ dẫn do lệnh TIMTRANG tạo ra.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "TKDMBV",
                    Title = "Thống kê danh mục bản vẽ",
                    Description = "Quét các khung tên trên bản vẽ và tạo bảng mục lục bản vẽ chuẩn AutoCAD Table.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "SYNREV",
                    Title = "Đồng bộ số lần thay đổi bản vẽ (Revision)",
                    Description = "Cập nhật hàng loạt chỉ số Revision cho toàn bộ các khung tên được chọn.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "SYNTF",
                    Title = "Đồng bộ mũi tên TO-FROM",
                    Description = "Tự động liên kết và cập nhật địa chỉ trang nguồn / trang đích giữa các cặp mũi tên To/From.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "GBV",
                    Title = "Ghép bản vẽ (Outline Frame)",
                    Description = "Ghép nhiều khung tên thành layout trình bày hoàn chỉnh.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "TBV",
                    Title = "Tách bản vẽ",
                    Description = "Tách từng khung tên thành file bản vẽ DWG riêng biệt theo số trang.",
                    Category = "Khung tên & Trang"
                },
                new CommandItem
                {
                    Code = "INSGEN",
                    Title = "Chèn ký hiệu gen số dây",
                    Description = "Chèn block đánh số gen dây tuần tự và hỗ trợ bắt điểm thông minh.",
                    Category = "Dây & Cầu đấu"
                },
                new CommandItem
                {
                    Code = "TKGEN",
                    Title = "Thống kê danh sách gen số",
                    Description = "Thống kê tất cả các gen số dây, gom nhóm số lượng và xuất bảng hoặc Excel.",
                    Category = "Dây & Cầu đấu"
                },
                new CommandItem
                {
                    Code = "SYNGEN",
                    Title = "Đồng bộ gen số theo trang bản vẽ",
                    Description = "Đánh lại số gen dây có gắn tiền tố là số trang của khung tên chứa nó.",
                    Category = "Dây & Cầu đấu"
                },
                new CommandItem
                {
                    Code = "TKCD",
                    Title = "Thống kê cầu đấu dây (Terminal Block)",
                    Description = "Thống kê các trạm cầu đấu domino, gom nhóm theo tên trạm và số chân.",
                    Category = "Dây & Cầu đấu"
                },
                new CommandItem
                {
                    Code = "SYNCD",
                    Title = "Đồng bộ tên cầu đấu theo trang",
                    Description = "Cập nhật tiền tố tên trạm cầu đấu theo số trang bản vẽ tương ứng.",
                    Category = "Dây & Cầu đấu"
                },
                new CommandItem
                {
                    Code = "TKPLCIN",
                    Title = "Thống kê địa chỉ DI-PLC",
                    Description = "Tạo bảng danh sách đầu vào Digital Input PLC (X0, X1, I0.0, I0.1...) từ bản vẽ.",
                    Category = "PLC I/O"
                },
                new CommandItem
                {
                    Code = "TKPLCOUT",
                    Title = "Thống kê địa chỉ DO-PLC",
                    Description = "Tạo bảng danh sách đầu ra Digital Output PLC (Y0, Y1, Q0.0, Q0.1...) từ bản vẽ.",
                    Category = "PLC I/O"
                },
                new CommandItem
                {
                    Code = "SYNPLC",
                    Title = "Đồng bộ địa chỉ DI/DO PLC",
                    Description = "Đồng bộ mã ký hiệu địa chỉ PLC giữa sơ đồ nguyên lý và bảng I/O.",
                    Category = "PLC I/O"
                },
                new CommandItem
                {
                    Code = "CAD2EXCEL",
                    Title = "Xuất dữ liệu Attribute / Text ra Excel",
                    Description = "Trích xuất thông tin các block thuộc tính hoặc text ra file CSV/Excel.",
                    Category = "Soạn thảo"
                },
                new CommandItem
                {
                    Code = "TKDMTB",
                    Title = "Thống kê danh mục thiết bị (BOM)",
                    Description = "Tạo bảng BOM tổng hợp thiết bị vật tư điện từ các block trên bản vẽ.",
                    Category = "Soạn thảo"
                },
                new CommandItem
                {
                    Code = "TAOBLOCKMAU",
                    Title = "Khởi tạo các Block điện mẫu chuẩn",
                    Description = "Tạo nhanh các block: GEN_SO, CAU_DAU, KHUNG_TEN, RL_COIL, RL_NO, PLC_DI vào bản vẽ hiện tại.",
                    Category = "Soạn thảo"
                }
            };

            ApplyFilter();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            ApplyFilter();
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (ItemsCommands == null || _allCommands == null) return;

            string keyword = TxtSearch?.Text?.Trim().ToLowerInvariant() ?? "";

            string selectedCategory = "";
            if (RbFilterDrafting?.IsChecked == true) selectedCategory = "Đánh số & Soạn thảo";
            else if (RbFilterCrossRef?.IsChecked == true) selectedCategory = "Relay & Khung 14 chân";
            else if (RbFilterSheet?.IsChecked == true) selectedCategory = "Khung tên & Trang";
            else if (RbFilterWire?.IsChecked == true) selectedCategory = "Dây & Cầu đấu";
            else if (RbFilterPlc?.IsChecked == true) selectedCategory = "PLC I/O";

            var filtered = _allCommands.Where(cmd =>
            {
                bool matchesCategory = string.IsNullOrEmpty(selectedCategory) || cmd.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase);
                if (!matchesCategory) return false;

                if (string.IsNullOrEmpty(keyword)) return true;

                return cmd.Code.ToLowerInvariant().Contains(keyword)
                    || cmd.Title.ToLowerInvariant().Contains(keyword)
                    || cmd.Description.ToLowerInvariant().Contains(keyword);
            }).ToList();

            ItemsCommands.ItemsSource = filtered;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cmdCode)
            {
                Close();
                var doc = CadDatabaseHelper.ActiveDoc;
                doc?.SendStringToExecute($"{cmdCode}\n", true, false, false);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cmdCode)
            {
                Clipboard.SetText(cmdCode);
                string original = btn.Content.ToString() ?? "Copy";
                btn.Content = "Đã copy!";
                btn.IsEnabled = false;

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5)
                };
                timer.Tick += (s, ev) =>
                {
                    btn.Content = original;
                    btn.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
