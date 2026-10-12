using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.UI.Mvvm;

namespace CadElectricalToolkit.UI.ViewModels
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
    /// ViewModel cho cửa sổ thông tin Plugin (ToolInfoWindow).
    /// Quản lý danh sách lệnh, tìm kiếm, lọc theo danh mục, và chạy/copy lệnh.
    /// </summary>
    public class ToolInfoViewModel : ViewModelBase
    {
        private readonly List<CommandItem> _allCommands;

        /// <summary>
        /// Reference đến Window (để đóng khi cần)
        /// </summary>
        public Window? OwnerWindow { get; set; }

        public ToolInfoViewModel()
        {
            _allCommands = BuildCommandList();

            RunCommand = new RelayCommand(param => ExecuteRun(param as string));
            CopyCommand = new RelayCommand(param => ExecuteCopy(param));
            CloseCommand = new RelayCommand(() => OwnerWindow?.Close());
            SelectCategoryCommand = new RelayCommand(param => SelectedCategory = param as string ?? "");

            ApplyFilter();
        }

        public int TotalCommandCount => _allCommands.Count;
        public int FilteredCount => FilteredCommands.Count;

        #region Search & Filter

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(IsSearchPlaceholderVisible));
                    ApplyFilter();
                }
            }
        }

        public bool IsSearchPlaceholderVisible => string.IsNullOrEmpty(_searchText);

        private string _selectedCategory = "";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) ApplyFilter(); }
        }

        private ObservableCollection<CommandItem> _filteredCommands = new();
        public ObservableCollection<CommandItem> FilteredCommands
        {
            get => _filteredCommands;
            set => SetProperty(ref _filteredCommands, value);
        }

        private void ApplyFilter()
        {
            string keyword = _searchText?.Trim().ToLowerInvariant() ?? "";

            var filtered = _allCommands.Where(cmd =>
            {
                bool matchesCategory = string.IsNullOrEmpty(_selectedCategory)
                    || cmd.Category.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase);
                if (!matchesCategory) return false;
                if (string.IsNullOrEmpty(keyword)) return true;

                return cmd.Code.ToLowerInvariant().Contains(keyword)
                    || cmd.Title.ToLowerInvariant().Contains(keyword)
                    || cmd.Description.ToLowerInvariant().Contains(keyword);
            });

            FilteredCommands = new ObservableCollection<CommandItem>(filtered);
            OnPropertyChanged(nameof(FilteredCount));
        }

        #endregion

        #region Commands

        public ICommand RunCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        private void ExecuteRun(string? cmdCode)
        {
            if (string.IsNullOrWhiteSpace(cmdCode)) return;
            OwnerWindow?.Close();
            var doc = CadDatabaseHelper.ActiveDoc;
            doc?.SendStringToExecute($"{cmdCode}\n", true, false, false);
        }

        private void ExecuteCopy(object? param)
        {
            if (param is string cmdCode && !string.IsNullOrEmpty(cmdCode))
            {
                Clipboard.SetText(cmdCode);
            }
        }

        #endregion

        #region Command List Data

        private static List<CommandItem> BuildCommandList()
        {
            return new List<CommandItem>
            {
                new() { Code = "DSTT", Title = "Đánh số thứ tự tăng dần", Description = "Giao diện WPF: Đánh số Block Attribute, Text/MText, tuỳ chỉnh Prefix, Suffix, From, bước nhảy và sắp xếp hướng tự động.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "DSBV", Title = "Đánh số thứ tự khung tên/bản vẽ", Description = "Giao diện WPF: Quản lý số trang, định dạng trang hiện tại/tổng trang (A00='01', TSHEET='/24'), tự động đếm tổng số trang.", Category = "Khung tên & Trang" },
                new() { Code = "DOIATT", Title = "Đổi giá trị Attribute hàng loạt", Description = "Giao diện WPF: Chọn block mẫu trên CAD để tự nạp danh sách TAG (ví dụ: TERM01, TERM02, MODEL...), gán giá trị mới hoặc Tìm & Thay thế hàng loạt.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "XOAYATT", Title = "Xoay góc Attribute hàng loạt", Description = "Giao diện WPF: Chọn góc xoay (90° dọc đứng, 270°, 0°, 180° hoặc lấy góc từ CAD) và quét chọn các block để xoay tag hàng loạt hoặc click từng cái.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "CDN", Title = "Cộng Dimension nhanh", Description = "Tính tổng chiều dài tất cả các kích thước Dimension được chọn và tự sao chép kết quả vào Clipboard.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "GTD", Title = "Gom Text dọc (Gióng hàng thẳng đứng)", Description = "Căn chỉnh các đối tượng Text/MText thẳng hàng theo phương dọc với khoảng cách đều nhau.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "GTN", Title = "Gom Text ngang (Gióng hàng nằm ngang)", Description = "Căn chỉnh các đối tượng Text/MText thẳng hàng theo phương ngang với khoảng cách đều nhau.", Category = "Đánh số & Soạn thảo" },
                new() { Code = "TCCTB", Title = "Đánh địa chỉ cuộn coil cho tiếp điểm", Description = "Tự động truy xuất vị trí cuộn coil (SheetNo-RowCol) và ghi vào thuộc tính ADDRESS_COIL của các tiếp điểm NO/NC.", Category = "Relay & Khung 14 chân" },
                new() { Code = "TKCRL", Title = "Thống kê chân số Relay / Contactor", Description = "Tạo bảng tham chiếu KHUNG 14 CHAN, tự động tính tọa độ các cặp chân tiếp điểm và cho phép đặt vị trí trực quan.", Category = "Relay & Khung 14 chân" },
                new() { Code = "SYNCRL", Title = "Đồng bộ tiếp điểm & cuộn coil 2 chiều", Description = "Cập nhật đồng thời: Coil -> ADDRESS_COIL trên tiếp điểm, và Tiếp điểm -> ADDRESS_1..4 trên Khung 14 chân.", Category = "Relay & Khung 14 chân" },
                new() { Code = "KTCRL", Title = "Kiểm tra chân số thiết bị", Description = "Phát hiện tiếp điểm mồ côi (không có coil), kiểm tra trùng lặp cặp chân hoặc vượt quá số lượng tiếp điểm cho phép.", Category = "Relay & Khung 14 chân" },
                new() { Code = "TIMTRANG", Title = "Tìm trang & vẽ đường chỉ dẫn", Description = "Click vào tiếp điểm / mũi tên để tìm trang đích và vẽ đường dóng trực tiếp đến khung tên trang đó.", Category = "Khung tên & Trang" },
                new() { Code = "XOALINK", Title = "Xóa đường chỉ dẫn truy vết trang", Description = "Dọn dẹp nhanh tất cả các đường mũi tên/line chỉ dẫn do lệnh TIMTRANG tạo ra.", Category = "Khung tên & Trang" },
                new() { Code = "TKDMBV", Title = "Thống kê danh mục bản vẽ", Description = "Quét các khung tên trên bản vẽ và tạo bảng mục lục bản vẽ chuẩn AutoCAD Table.", Category = "Khung tên & Trang" },
                new() { Code = "SYNREV", Title = "Đồng bộ số lần thay đổi bản vẽ (Revision)", Description = "Cập nhật hàng loạt chỉ số Revision cho toàn bộ các khung tên được chọn.", Category = "Khung tên & Trang" },
                new() { Code = "SYNTF", Title = "Đồng bộ mũi tên TO-FROM (DANDAY)", Description = "Mở giao diện Tab 5 đánh thông tin địa chỉ trang/tọa độ lưới cho dây dẫn từ sheet này sang sheet khác hàng loạt.", Category = "Khung tên & Trang" },
                new() { Code = "GBV", Title = "Ghép bản vẽ (Outline Frame)", Description = "Ghép nhiều khung tên thành layout trình bày hoàn chỉnh.", Category = "Khung tên & Trang" },
                new() { Code = "TBV", Title = "Tách bản vẽ", Description = "Tách từng khung tên thành file bản vẽ DWG riêng biệt theo số trang.", Category = "Khung tên & Trang" },
                new() { Code = "INSGEN", Title = "Chèn ký hiệu gen số dây", Description = "Chèn block đánh số gen dây tuần tự và hỗ trợ bắt điểm thông minh.", Category = "Dây & Cầu đấu" },
                new() { Code = "TKGEN", Title = "Thống kê danh sách gen số", Description = "Thống kê tất cả các gen số dây, gom nhóm số lượng và xuất bảng hoặc Excel.", Category = "Dây & Cầu đấu" },
                new() { Code = "SYNGEN", Title = "Đồng bộ gen số theo trang bản vẽ", Description = "Đánh lại số gen dây có gắn tiền tố là số trang của khung tên chứa nó.", Category = "Dây & Cầu đấu" },
                new() { Code = "TKCD", Title = "Thống kê cầu đấu dây (Terminal Block)", Description = "Thống kê các trạm cầu đấu domino, gom nhóm theo tên trạm và số chân.", Category = "Dây & Cầu đấu" },
                new() { Code = "SYNCD", Title = "Đồng bộ tên cầu đấu theo trang", Description = "Cập nhật tiền tố tên trạm cầu đấu theo số trang bản vẽ tương ứng.", Category = "Dây & Cầu đấu" },
                new() { Code = "TKPLCIN", Title = "Thống kê địa chỉ DI-PLC", Description = "Tạo bảng danh sách đầu vào Digital Input PLC (X0, X1, I0.0, I0.1...) từ bản vẽ.", Category = "PLC I/O" },
                new() { Code = "TKPLCOUT", Title = "Thống kê địa chỉ DO-PLC", Description = "Tạo bảng danh sách đầu ra Digital Output PLC (Y0, Y1, Q0.0, Q0.1...) từ bản vẽ.", Category = "PLC I/O" },
                new() { Code = "SYNPLC", Title = "Đồng bộ địa chỉ DI/DO PLC", Description = "Đồng bộ mã ký hiệu địa chỉ PLC giữa sơ đồ nguyên lý và bảng I/O.", Category = "PLC I/O" },
                new() { Code = "CAD2EXCEL", Title = "Xuất dữ liệu Attribute / Text ra Excel", Description = "Trích xuất thông tin các block thuộc tính hoặc text ra file CSV/Excel.", Category = "Soạn thảo" },
                new() { Code = "TKDMTB", Title = "Thống kê danh mục thiết bị (BOM)", Description = "Tạo bảng BOM tổng hợp thiết bị vật tư điện từ các block trên bản vẽ.", Category = "Soạn thảo" },
                new() { Code = "TAOBLOCKMAU", Title = "Khởi tạo các Block điện mẫu chuẩn", Description = "Tạo nhanh các block: GEN_SO, CAU_DAU, KHUNG_TEN, RL_COIL, RL_NO, PLC_DI vào bản vẽ hiện tại.", Category = "Soạn thảo" },
            };
        }

        #endregion
    }
}
