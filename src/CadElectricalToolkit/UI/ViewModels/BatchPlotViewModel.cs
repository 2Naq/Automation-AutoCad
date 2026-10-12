using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.UI.Mvvm;
using CadElectricalToolkit.UI.Services;
using Microsoft.Win32;

namespace CadElectricalToolkit.UI.ViewModels
{
    public class PlotSheetItemViewModel : ViewModelBase
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    OnSelectionChanged?.Invoke();
                }
            }
        }

        public Action? OnSelectionChanged { get; set; }

        public int OrderIndex { get; set; }
        public string SheetNo { get; set; } = "";
        public string DrawingTitle { get; set; } = "";
        public string PaperSizeEstimate { get; set; } = "A4";
        public string Dimensions { get; set; } = "";
        public PlotSheetItem SheetItem { get; set; } = null!;

        public PlotSheetItemViewModel(PlotSheetItem item, Action? onSelectionChanged = null)
        {
            SheetItem = item;
            OrderIndex = item.OrderIndex;
            SheetNo = item.SheetNo;
            DrawingTitle = item.DrawingTitle;
            PaperSizeEstimate = item.PaperSizeEstimate;
            Dimensions = $"{Math.Round(item.Width)} x {Math.Round(item.Height)} mm";
            IsSelected = item.IsSelected;
            OnSelectionChanged = onSelectionChanged;
        }
    }

    public class BatchPlotViewModel : ViewModelBase
    {
        private readonly ICadInteractionService _cadService;
        private Window? _ownerWindow;

        public BatchPlotViewModel(ICadInteractionService cadService)
        {
            _cadService = cadService;

            // Khởi tạo commands
            SelectAllCommand = new RelayCommand(SelectAll);
            UnselectAllCommand = new RelayCommand(UnselectAll);
            SortBySheetNoCommand = new RelayCommand(SortBySheetNo);
            SortByCoordCommand = new RelayCommand(SortByCoordinates);
            ScanAllCommand = new RelayCommand(ScanAllFrames);
            PickFramesCommand = new RelayCommand(PickFrames);
            BrowseOutputFileCommand = new RelayCommand(BrowseOutputFile);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            StartPlotCommand = new RelayCommand(ExecutePlot, () => !IsPlotting && Sheets.Any(s => s.IsSelected));

            // Tải dữ liệu ban đầu
            LoadPrintersAndStyles();
            ScanAllFrames();
        }

        public void SetOwnerWindow(Window window)
        {
            _ownerWindow = window;
        }

        #region Danh sách Bản vẽ (Sheets)

        public ObservableCollection<PlotSheetItemViewModel> Sheets { get; } = new();

        private string _selectedCountText = "Chưa có bản vẽ nào";
        public string SelectedCountText
        {
            get => _selectedCountText;
            set => SetProperty(ref _selectedCountText, value);
        }

        private void UpdateSelectedCount()
        {
            int selected = Sheets.Count(s => s.IsSelected);
            int total = Sheets.Count;
            SelectedCountText = $"Đã chọn: {selected} / {total} bản vẽ";
            RelayCommand.RaiseCanExecuteChanged();
        }

        public RelayCommand SelectAllCommand { get; }
        public RelayCommand UnselectAllCommand { get; }
        public RelayCommand SortBySheetNoCommand { get; }
        public RelayCommand SortByCoordCommand { get; }
        public RelayCommand ScanAllCommand { get; }
        public RelayCommand PickFramesCommand { get; }

        private void SelectAll()
        {
            foreach (var s in Sheets) s.IsSelected = true;
            UpdateSelectedCount();
        }

        private void UnselectAll()
        {
            foreach (var s in Sheets) s.IsSelected = false;
            UpdateSelectedCount();
        }

        private void SortBySheetNo()
        {
            var rawList = Sheets.Select(s => s.SheetItem).ToList();
            var sorted = BatchPlotHelper.SortSheets(rawList, true);
            ApplySheetsList(sorted);
        }

        private void SortByCoordinates()
        {
            var rawList = Sheets.Select(s => s.SheetItem).ToList();
            var sorted = BatchPlotHelper.SortSheets(rawList, false);
            ApplySheetsList(sorted);
        }

        public void ScanAllFrames()
        {
            var list = _cadService.ScanAllFramesForPlotting();
            if (list.Count == 0 && !_cadService.IsAvailable)
            {
                // Dữ liệu mẫu phục vụ kiểm thử ngoài AutoCAD
                list = GenerateSampleSheets();
            }
            ApplySheetsList(list);
        }

        public void PickFrames()
        {
            if (_ownerWindow == null) return;
            var list = _cadService.PickFramesForPlotting(_ownerWindow);
            if (list != null && list.Count > 0)
            {
                ApplySheetsList(list);
            }
        }

        private void ApplySheetsList(List<PlotSheetItem> list)
        {
            Sheets.Clear();
            foreach (var item in list)
            {
                var vm = new PlotSheetItemViewModel(item, UpdateSelectedCount);
                Sheets.Add(vm);
            }
            UpdateSelectedCount();
            if (string.IsNullOrWhiteSpace(OutputFilePath))
            {
                OutputFilePath = _cadService.GetDefaultOutputPdfPath();
            }
        }

        private List<PlotSheetItem> GenerateSampleSheets()
        {
            return new List<PlotSheetItem>
            {
                new PlotSheetItem { OrderIndex = 1, SheetNo = "01", DrawingTitle = "MẠCH NGUỒN TỔNG & PHÂN PHỐI", Extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Autodesk.AutoCAD.Geometry.Point3d(0, 0, 0), new Autodesk.AutoCAD.Geometry.Point3d(297, 210, 0)) },
                new PlotSheetItem { OrderIndex = 2, SheetNo = "02", DrawingTitle = "MẠCH ĐIỀU KHIỂN ĐỘNG CƠ M1-M4", Extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Autodesk.AutoCAD.Geometry.Point3d(350, 0, 0), new Autodesk.AutoCAD.Geometry.Point3d(647, 210, 0)) },
                new PlotSheetItem { OrderIndex = 3, SheetNo = "03", DrawingTitle = "MODULE PLC DIGITAL INPUT (DI)", Extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Autodesk.AutoCAD.Geometry.Point3d(700, 0, 0), new Autodesk.AutoCAD.Geometry.Point3d(997, 210, 0)) },
                new PlotSheetItem { OrderIndex = 4, SheetNo = "04", DrawingTitle = "MODULE PLC DIGITAL OUTPUT (DO)", Extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Autodesk.AutoCAD.Geometry.Point3d(1050, 0, 0), new Autodesk.AutoCAD.Geometry.Point3d(1347, 210, 0)) },
                new PlotSheetItem { OrderIndex = 5, SheetNo = "05", DrawingTitle = "SƠ ĐỒ ĐẤU NỐI CẦU ĐẤU TB1", Extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Autodesk.AutoCAD.Geometry.Point3d(1400, 0, 0), new Autodesk.AutoCAD.Geometry.Point3d(1697, 210, 0)) }
            };
        }

        #endregion

        #region Cấu hình Máy in & Khổ giấy (Plot Configuration)

        public ObservableCollection<string> Devices { get; } = new();

        private string _selectedDevice = "AutoCAD PDF (General Documentation).pc3";
        public string SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    LoadMediaList();
                }
            }
        }

        public ObservableCollection<string> MediaList { get; } = new();

        private string _selectedMedia = "ISO_full_bleed_A4_(297.00_x_210.00_MM)";
        public string SelectedMedia
        {
            get => _selectedMedia;
            set => SetProperty(ref _selectedMedia, value);
        }

        private bool _autoMatchMediaSize = true;
        public bool AutoMatchMediaSize
        {
            get => _autoMatchMediaSize;
            set => SetProperty(ref _autoMatchMediaSize, value);
        }

        private void LoadPrintersAndStyles()
        {
            Devices.Clear();
            var devList = _cadService.GetAvailablePlotDevices();
            foreach (var d in devList) Devices.Add(d);
            if (Devices.Count > 0 && !Devices.Contains(_selectedDevice))
            {
                var preferred = Devices.FirstOrDefault(d => string.Equals(d, "DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase))
                             ?? Devices.FirstOrDefault(d => d.IndexOf("AutoCAD PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                             ?? Devices.FirstOrDefault(d => d.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                             ?? Devices[0];
                _selectedDevice = preferred;
                OnPropertyChanged(nameof(SelectedDevice));
            }

            LoadMediaList();

            AvailableStyleSheets.Clear();
            var ctbList = _cadService.GetAvailableStyleSheets();
            foreach (var c in ctbList) AvailableStyleSheets.Add(c);
            if (AvailableStyleSheets.Count > 0 && !AvailableStyleSheets.Contains(_selectedStyleSheet))
            {
                var preferredCtb = AvailableStyleSheets.FirstOrDefault(c => string.Equals(c, "monochrome.ctb", StringComparison.OrdinalIgnoreCase))
                                ?? AvailableStyleSheets[0];
                _selectedStyleSheet = preferredCtb;
                OnPropertyChanged(nameof(SelectedStyleSheet));
            }
        }

        private void LoadMediaList()
        {
            MediaList.Clear();
            var media = _cadService.GetMediaNamesForDevice(_selectedDevice);
            foreach (var m in media) MediaList.Add(m);

            if (MediaList.Count > 0)
            {
                // Ưu tiên chọn khổ A4 ngang tràn lề
                var defaultMedia = MediaList.FirstOrDefault(m => m.IndexOf("full_bleed_A4_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                                ?? MediaList.FirstOrDefault(m => m.IndexOf("A4_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                                ?? MediaList[0];
                SelectedMedia = defaultMedia;
            }
        }

        #endregion

        #region Kiểu in (Plot Style / CTB)

        private bool _isMonochrome = true;
        public bool IsMonochrome
        {
            get => _isMonochrome;
            set
            {
                if (SetProperty(ref _isMonochrome, value) && value)
                {
                    _isColor = false;
                    _isCustomCtb = false;
                    OnPropertyChanged(nameof(IsColor));
                    OnPropertyChanged(nameof(IsCustomCtb));
                }
            }
        }

        private bool _isColor = false;
        public bool IsColor
        {
            get => _isColor;
            set
            {
                if (SetProperty(ref _isColor, value) && value)
                {
                    _isMonochrome = false;
                    _isCustomCtb = false;
                    OnPropertyChanged(nameof(IsMonochrome));
                    OnPropertyChanged(nameof(IsCustomCtb));
                }
            }
        }

        private bool _isCustomCtb = false;
        public bool IsCustomCtb
        {
            get => _isCustomCtb;
            set
            {
                if (SetProperty(ref _isCustomCtb, value) && value)
                {
                    _isMonochrome = false;
                    _isColor = false;
                    OnPropertyChanged(nameof(IsMonochrome));
                    OnPropertyChanged(nameof(IsColor));
                }
            }
        }

        public ObservableCollection<string> AvailableStyleSheets { get; } = new();

        private string _selectedStyleSheet = "monochrome.ctb";
        public string SelectedStyleSheet
        {
            get => _selectedStyleSheet;
            set => SetProperty(ref _selectedStyleSheet, value);
        }

        private string GetEffectivePlotStyle()
        {
            if (IsMonochrome) return "monochrome.ctb";
            if (IsColor) return "acad.ctb";
            return SelectedStyleSheet;
        }

        #endregion

        #region Tùy chọn Xuất File & Ghép Trang (Output & Merge Options)

        private bool _mergeIntoSinglePdf = true;
        public bool MergeIntoSinglePdf
        {
            get => _mergeIntoSinglePdf;
            set
            {
                if (SetProperty(ref _mergeIntoSinglePdf, value))
                {
                    OnPropertyChanged(nameof(IsIndividualFiles));
                }
            }
        }

        public bool IsIndividualFiles
        {
            get => !_mergeIntoSinglePdf;
            set => MergeIntoSinglePdf = !value;
        }

        private string _outputFilePath = "";
        public string OutputFilePath
        {
            get => _outputFilePath;
            set => SetProperty(ref _outputFilePath, value);
        }

        private string _outputFolder = "";
        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        private bool _openAfterPlot = true;
        public bool OpenAfterPlot
        {
            get => _openAfterPlot;
            set => SetProperty(ref _openAfterPlot, value);
        }

        public RelayCommand BrowseOutputFileCommand { get; }
        public RelayCommand BrowseOutputFolderCommand { get; }

        private void BrowseOutputFile()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Chọn vị trí lưu file PDF tổng hợp",
                Filter = "Tài liệu PDF (*.pdf)|*.pdf",
                FileName = Path.GetFileName(OutputFilePath)
            };

            if (!string.IsNullOrWhiteSpace(OutputFilePath))
            {
                string? dir = Path.GetDirectoryName(OutputFilePath);
                if (Directory.Exists(dir)) sfd.InitialDirectory = dir;
            }

            if (sfd.ShowDialog() == true)
            {
                OutputFilePath = sfd.FileName;
            }
        }

        private void BrowseOutputFolder()
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu các file PDF từng bản vẽ";
                if (Directory.Exists(OutputFolder)) fbd.SelectedPath = OutputFolder;

                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputFolder = fbd.SelectedPath;
                }
            }
        }

        #endregion

        #region TÍNH NĂNG ĐẶC BIỆT: Liên kết Nhảy Trang PDF Tương Tác (Smart Navigation Hyperlinks)

        private bool _enableInteractiveHyperlinks = true;
        public bool EnableInteractiveHyperlinks
        {
            get => _enableInteractiveHyperlinks;
            set => SetProperty(ref _enableInteractiveHyperlinks, value);
        }

        private bool _matchBaseSignalOnly = true;
        public bool MatchBaseSignalOnly
        {
            get => _matchBaseSignalOnly;
            set => SetProperty(ref _matchBaseSignalOnly, value);
        }

        #endregion

        #region Tiến trình & Thực thi In (Plot Execution & Progress)

        private bool _isPlotting = false;
        public bool IsPlotting
        {
            get => _isPlotting;
            set
            {
                if (SetProperty(ref _isPlotting, value))
                {
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private double _progressPercentage = 0;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private string _progressStatus = "";
        public string ProgressStatus
        {
            get => _progressStatus;
            set => SetProperty(ref _progressStatus, value);
        }

        private string _statusMessage = "Sẵn sàng in.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isSuccess = false;
        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        public RelayCommand StartPlotCommand { get; }

        private async void ExecutePlot()
        {
            var selectedSheets = Sheets.Where(s => s.IsSelected).Select(s => s.SheetItem).ToList();
            if (selectedSheets.Count == 0)
            {
                StatusMessage = "Vui lòng chọn ít nhất 1 bản vẽ để in.";
                return;
            }

            IsPlotting = true;
            ProgressPercentage = 0;
            ProgressStatus = "Đang khởi tạo lệnh in...";
            StatusMessage = "Đang xử lý...";
            IsSuccess = false;

            var options = new BatchPlotOptions
            {
                DeviceName = SelectedDevice,
                MediaName = SelectedMedia,
                AutoMatchMediaSize = AutoMatchMediaSize,
                PlotStyleSheet = GetEffectivePlotStyle(),
                IsMonochrome = IsMonochrome,
                MergeIntoSinglePdf = MergeIntoSinglePdf,
                EnableInteractiveHyperlinks = EnableInteractiveHyperlinks,
                MatchBaseSignalOnly = MatchBaseSignalOnly,
                OutputFilePath = OutputFilePath,
                OutputFolder = OutputFolder,
                OpenAfterPlot = OpenAfterPlot
            };

            var progress = new Progress<PlotProgressReport>(report =>
            {
                ProgressPercentage = report.Percentage;
                ProgressStatus = report.StatusMessage;

                try
                {
                    Application.Current?.Dispatcher?.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                }
                catch { }
            });

            try
            {
                await System.Threading.Tasks.Task.Yield();
                var result = _cadService.ExecuteBatchPlot(selectedSheets, options, progress);

                if (result.Success)
                {
                    IsSuccess = true;
                    ProgressPercentage = 100;
                    if (MergeIntoSinglePdf)
                    {
                        string linkInfo = options.EnableInteractiveHyperlinks && result.TotalLinksCreated > 0
                            ? $" (Đã tạo {result.TotalLinksCreated} liên kết nhảy trang tương tác!)"
                            : "";
                        ProgressStatus = $"Hoàn tất! Đã ghép {result.TotalPagesPlotted} trang vào file PDF{linkInfo}";
                        StatusMessage = $"Xuất file thành công: {Path.GetFileName(result.OutputFilePath)}";
                    }
                    else
                    {
                        ProgressStatus = $"Hoàn tất! Đã xuất {result.TotalPagesPlotted} file PDF thành công.";
                        StatusMessage = $"Lưu tại: {result.OutputFilePath}";
                    }
                }
                else
                {
                    IsSuccess = false;
                    ProgressStatus = "In thất bại.";
                    StatusMessage = result.ErrorMessage;
                    MessageBox.Show(result.ErrorMessage, "Lỗi In Bản Vẽ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                ProgressStatus = "Lỗi ngoại lệ: " + ex.Message;
                StatusMessage = ex.Message;
            }
            finally
            {
                IsPlotting = false;
            }
        }

        #endregion
    }
}
