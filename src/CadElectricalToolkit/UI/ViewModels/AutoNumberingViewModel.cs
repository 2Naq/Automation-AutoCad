using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadElectricalToolkit.UI.Mvvm;
using CadElectricalToolkit.UI.Services;

namespace CadElectricalToolkit.UI.ViewModels
{
    /// <summary>
    /// ViewModel quản lý toàn bộ trạng thái và logic cho cửa sổ Đánh số thứ tự (AutoNumberingWindow).
    /// Gồm 3 tab: STT Khung tên, STT Block Attribute, Đổi Attribute hàng loạt.
    /// 
    /// Kiến trúc MVVM:
    ///   View (XAML) ← Binding → ViewModel (file này) → Service (ICadInteractionService) → AutoCAD API
    /// </summary>
    public class AutoNumberingViewModel : ViewModelBase
    {
        private readonly ICadInteractionService _cadService;

        /// <summary>
        /// Reference đến Window (cần cho StartUserInteraction của AutoCAD).
        /// Được gán bởi View khi khởi tạo ViewModel.
        /// </summary>
        public Window? OwnerWindow { get; set; }

        public AutoNumberingViewModel(ICadInteractionService cadService)
        {
            _cadService = cadService;

            // Tab 1: Frame
            FrameChooseTagCommand = new RelayCommand(ExecuteFrameChooseTag);
            FrameSelectBlocksCommand = new RelayCommand(ExecuteFrameSelectBlocks);

            // Tab 2: Block Attr
            AttrChooseTagCommand = new RelayCommand(ExecuteAttrChooseTag);
            AttrSelectBlocksCommand = new RelayCommand(ExecuteAttrSelectBlocks);
            AttrPickOneByOneCommand = new RelayCommand(ExecuteAttrPickOneByOne);

            // Tab 3: Batch
            BatchPickSampleCommand = new RelayCommand(ExecuteBatchPickSample);
            BatchClearBlockNameCommand = new RelayCommand(() => BatchBlockName = "");
            BatchApplyCommand = new RelayCommand(ExecuteBatchApply);
            BatchPickOneByOneCommand = new RelayCommand(ExecuteBatchPickOneByOne);

            // Tab 4: Rotate
            RotatePickSampleCommand = new RelayCommand(ExecuteRotatePickSample);
            RotateClearBlockNameCommand = new RelayCommand(() => RotateBlockName = "");
            RotatePickTagCommand = new RelayCommand(ExecuteRotatePickTag);
            RotatePickAngleCommand = new RelayCommand(ExecuteRotatePickAngle);
            RotateSetAnglePresetCommand = new RelayCommand(p => ExecuteRotateSetPreset(p?.ToString()));
            RotateApplyCommand = new RelayCommand(ExecuteRotateApply);
            RotatePickOneByOneCommand = new RelayCommand(ExecuteRotatePickOneByOne);

            // Close
            CloseCommand = new RelayCommand(ExecuteClose);
        }

        #region ═══════════════ TAB 1: Khung tên (Frame) ═══════════════

        private string _framePrefix = "";
        public string FramePrefix
        {
            get => _framePrefix;
            set { if (SetProperty(ref _framePrefix, value)) UpdateFramePreviews(); }
        }

        private string _frameSuffix = "";
        public string FrameSuffix
        {
            get => _frameSuffix;
            set { if (SetProperty(ref _frameSuffix, value)) UpdateFramePreviews(); }
        }

        private int _frameFromNumber = 1;
        public string FrameFromText
        {
            get => _frameFromNumber.ToString();
            set { if (int.TryParse(value, out int v)) { _frameFromNumber = v; OnPropertyChanged(); UpdateFramePreviews(); } }
        }

        private int _frameNumberStyleIndex = 0;
        public int FrameNumberStyleIndex
        {
            get => _frameNumberStyleIndex;
            set { if (SetProperty(ref _frameNumberStyleIndex, value)) UpdateFramePreviews(); }
        }

        private bool _frameTwoDigits = true;
        public bool FrameTwoDigits
        {
            get => _frameTwoDigits;
            set { if (SetProperty(ref _frameTwoDigits, value)) UpdateFramePreviews(); }
        }

        private string _frameBlockName = "Frame-a4";
        public string FrameBlockName
        {
            get => _frameBlockName;
            set => SetProperty(ref _frameBlockName, value);
        }

        private string _frameAttrTag = "A00";
        public string FrameAttrTag
        {
            get => _frameAttrTag;
            set => SetProperty(ref _frameAttrTag, value);
        }

        private int _frameDirectionIndex = 0;
        public int FrameDirectionIndex
        {
            get => _frameDirectionIndex;
            set => SetProperty(ref _frameDirectionIndex, value);
        }

        // Page Mode
        private bool _framePageMode = true;
        public bool FramePageMode
        {
            get => _framePageMode;
            set { if (SetProperty(ref _framePageMode, value)) UpdateFramePagePreview(); }
        }

        private bool _frameAutoTotal = true;
        public bool FrameAutoTotal
        {
            get => _frameAutoTotal;
            set { if (SetProperty(ref _frameAutoTotal, value)) UpdateFramePagePreview(); }
        }

        private string _frameTotalPagesTag = "TSHEET";
        public string FrameTotalPagesTag
        {
            get => _frameTotalPagesTag;
            set { if (SetProperty(ref _frameTotalPagesTag, value)) UpdateFramePagePreview(); }
        }

        private string _frameTotalPagesPrefix = "/";
        public string FrameTotalPagesPrefix
        {
            get => _frameTotalPagesPrefix;
            set { if (SetProperty(ref _frameTotalPagesPrefix, value)) UpdateFramePagePreview(); }
        }

        private int _frameTotalPages = 24;
        public string FrameTotalPagesText
        {
            get => _frameTotalPages.ToString();
            set { if (int.TryParse(value, out int v)) { _frameTotalPages = v; OnPropertyChanged(); UpdateFramePagePreview(); } }
        }

        // Previews
        private string _framePreview = "01";
        public string FramePreview
        {
            get => _framePreview;
            set => SetProperty(ref _framePreview, value);
        }

        private string _framePagePreview = "VD: A00='01', TSHEET='/24' (Hiển thị: 01 /24)";
        public string FramePagePreview
        {
            get => _framePagePreview;
            set => SetProperty(ref _framePagePreview, value);
        }

        private string _frameStatus = "";
        public string FrameStatus
        {
            get => _frameStatus;
            set => SetProperty(ref _frameStatus, value);
        }

        // Commands
        public ICommand FrameChooseTagCommand { get; }
        public ICommand FrameSelectBlocksCommand { get; }

        private void UpdateFramePreviews()
        {
            string numStr = FormatNumber(_frameFromNumber, _frameNumberStyleIndex, _frameTwoDigits);
            FramePreview = $"{_framePrefix}{numStr}{_frameSuffix}";
            UpdateFramePagePreview();
        }

        private void UpdateFramePagePreview()
        {
            if (!_framePageMode)
            {
                FramePagePreview = "Chế độ ghi tổng số trang đang tắt.";
                return;
            }

            string pageVal = $"{_framePrefix}{FormatNumber(_frameFromNumber, _frameNumberStyleIndex, _frameTwoDigits)}{_frameSuffix}";
            int totalNum = _frameAutoTotal ? 24 : _frameTotalPages;
            string totalVal = $"{_frameTotalPagesPrefix}{FormatNumber(totalNum, _frameNumberStyleIndex, _frameTwoDigits)}";
            string tag1 = !string.IsNullOrWhiteSpace(_frameAttrTag) ? _frameAttrTag : "A00";
            string tag2 = !string.IsNullOrWhiteSpace(_frameTotalPagesTag) ? _frameTotalPagesTag : "TSHEET";
            FramePagePreview = $"VD: {tag1}='{pageVal}', {tag2}='{totalVal}' (Hiển thị: {pageVal} {totalVal})";
        }

        private void ExecuteFrameChooseTag()
        {
            if (!CheckCadAvailable()) return;
            var result = _cadService.PickBlock(OwnerWindow!, "Chọn Block hoặc Attribute trên bản vẽ để lấy Block name và Tag: ");
            if (result == null) return;

            if (!result.IsText)
            {
                FrameBlockName = result.BlockName;
                if (!string.IsNullOrEmpty(result.ClosestTag)) FrameAttrTag = result.ClosestTag;
                FrameStatus = $"Đã nhận diện: {result.BlockName} (TAG: {result.ClosestTag})";
            }
        }

        private void ExecuteFrameSelectBlocks()
        {
            if (!CheckCadAvailable()) return;

            if (string.IsNullOrWhiteSpace(_frameAttrTag))
            {
                MessageBox.Show("Vui lòng nhập Attribute TAG cho số trang (ví dụ: A00).", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _cadService.NumberFrames(
                OwnerWindow!,
                _framePrefix, _frameSuffix, _frameFromNumber,
                _frameNumberStyleIndex, _frameTwoDigits,
                _frameBlockName, _frameAttrTag, _frameDirectionIndex,
                _framePageMode, _frameAutoTotal, _frameTotalPages,
                _frameTotalPagesTag, _frameTotalPagesPrefix);

            if (result.Count > 0)
            {
                FrameStatus = $"✅ Đã áp dụng {result.Count} khung tên vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
            }
        }

        #endregion

        #region ═══════════════ TAB 2: Block Attribute ═══════════════

        private string _attrPrefix = "";
        public string AttrPrefix
        {
            get => _attrPrefix;
            set { if (SetProperty(ref _attrPrefix, value)) UpdateAttrPreview(); }
        }

        private string _attrSuffix = "";
        public string AttrSuffix
        {
            get => _attrSuffix;
            set { if (SetProperty(ref _attrSuffix, value)) UpdateAttrPreview(); }
        }

        private int _attrFromNumber = 1;
        public string AttrFromText
        {
            get => _attrFromNumber.ToString();
            set { if (int.TryParse(value, out int v)) { _attrFromNumber = v; OnPropertyChanged(); UpdateAttrPreview(); } }
        }

        private int _attrStep = 1;
        public string AttrStepText
        {
            get => _attrStep.ToString();
            set { if (int.TryParse(value, out int v)) { _attrStep = Math.Max(1, v); OnPropertyChanged(); UpdateAttrPreview(); } }
        }

        private int _attrNumberStyleIndex = 0;
        public int AttrNumberStyleIndex
        {
            get => _attrNumberStyleIndex;
            set { if (SetProperty(ref _attrNumberStyleIndex, value)) UpdateAttrPreview(); }
        }

        private bool _attrTwoDigits = false;
        public bool AttrTwoDigits
        {
            get => _attrTwoDigits;
            set { if (SetProperty(ref _attrTwoDigits, value)) UpdateAttrPreview(); }
        }

        private string _attrBlockName = "";
        public string AttrBlockName
        {
            get => _attrBlockName;
            set => SetProperty(ref _attrBlockName, value);
        }

        private string _attrTagName = "";
        public string AttrTagName
        {
            get => _attrTagName;
            set => SetProperty(ref _attrTagName, value);
        }

        private int _attrDirectionIndex = 0;
        public int AttrDirectionIndex
        {
            get => _attrDirectionIndex;
            set => SetProperty(ref _attrDirectionIndex, value);
        }

        private bool _attrIncludeText = true;
        public bool AttrIncludeText
        {
            get => _attrIncludeText;
            set => SetProperty(ref _attrIncludeText, value);
        }

        private string _attrPreview = "1";
        public string AttrPreview
        {
            get => _attrPreview;
            set => SetProperty(ref _attrPreview, value);
        }

        private string _attrStatus = "";
        public string AttrStatus
        {
            get => _attrStatus;
            set => SetProperty(ref _attrStatus, value);
        }

        // Commands
        public ICommand AttrChooseTagCommand { get; }
        public ICommand AttrSelectBlocksCommand { get; }
        public ICommand AttrPickOneByOneCommand { get; }

        private void UpdateAttrPreview()
        {
            string numStr = FormatNumber(_attrFromNumber, _attrNumberStyleIndex, _attrTwoDigits);
            AttrPreview = $"{_attrPrefix}{numStr}{_attrSuffix}";
        }

        private void ExecuteAttrChooseTag()
        {
            if (!CheckCadAvailable()) return;
            var result = _cadService.PickBlock(OwnerWindow!, "Chọn Block hoặc Attribute trên bản vẽ để lấy Block name và Tag: ");
            if (result == null) return;

            if (result.IsText)
            {
                AttrBlockName = "";
                AttrTagName = "(TEXT/MTEXT)";
                AttrStatus = "Đã nhận diện đối tượng Text/MText.";
            }
            else
            {
                AttrBlockName = result.BlockName;
                if (!string.IsNullOrEmpty(result.ClosestTag)) AttrTagName = result.ClosestTag;
                AttrStatus = $"Đã nhận diện: {result.BlockName} (TAG: {result.ClosestTag})";
            }
        }

        private void ExecuteAttrSelectBlocks()
        {
            if (!CheckCadAvailable()) return;

            var result = _cadService.NumberBlockAttributes(
                OwnerWindow!,
                _attrPrefix, _attrSuffix, _attrFromNumber, _attrStep,
                _attrNumberStyleIndex, _attrTwoDigits,
                _attrBlockName, _attrTagName,
                _attrDirectionIndex, _attrIncludeText);

            if (result.Count > 0)
            {
                AttrStatus = $"✅ Đã áp dụng thành công {result.Count} đối tượng vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
                AttrFromText = result.NextNumber.ToString();
            }
        }

        private void ExecuteAttrPickOneByOne()
        {
            if (!CheckCadAvailable()) return;

            var result = _cadService.NumberOneByOne(
                OwnerWindow!,
                _attrPrefix, _attrSuffix, _attrFromNumber, _attrStep,
                _attrNumberStyleIndex, _attrTwoDigits, _attrTagName);

            if (result.Count > 0)
            {
                AttrStatus = $"✅ Đã áp dụng {result.Count} đối tượng vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
                AttrFromText = result.NextNumber.ToString();
            }
        }

        #endregion

        #region ═══════════════ TAB 3: Batch Edit ═══════════════

        private string _batchBlockName = "";
        public string BatchBlockName
        {
            get => _batchBlockName;
            set
            {
                if (SetProperty(ref _batchBlockName, value))
                {
                    UpdateBatchPreview();
                    if (!string.IsNullOrWhiteSpace(value) && _cadService.IsAvailable)
                    {
                        var tags = _cadService.GetBlockAttributeTags(value.Trim());
                        if (tags != null && tags.Count > 0)
                        {
                            BatchTagList = tags;
                            BatchSampleInfo = $"Block: '{value.Trim()}' ({tags.Count} TAGs)";
                            if (string.IsNullOrEmpty(_batchAttrTag) || !_batchTagList.Contains(_batchAttrTag, StringComparer.OrdinalIgnoreCase))
                            {
                                BatchAttrTag = tags[0];
                            }
                        }
                    }
                }
            }
        }

        private string _batchSampleInfo = "(Click để tự nạp Block & TAGs)";
        public string BatchSampleInfo
        {
            get => _batchSampleInfo;
            set => SetProperty(ref _batchSampleInfo, value);
        }

        private string _batchAttrTag = "";
        public string BatchAttrTag
        {
            get => _batchAttrTag;
            set { if (SetProperty(ref _batchAttrTag, value)) { UpdateBatchSampleVal(); UpdateBatchPreview(); } }
        }

        private List<string> _batchTagList = new();
        public List<string> BatchTagList
        {
            get => _batchTagList;
            set => SetProperty(ref _batchTagList, value);
        }

        private string _batchSampleVal = "(Chưa chọn mẫu)";
        public string BatchSampleVal
        {
            get => _batchSampleVal;
            set => SetProperty(ref _batchSampleVal, value);
        }

        private bool _batchModeDirect = true;
        public bool BatchModeDirect
        {
            get => _batchModeDirect;
            set { if (SetProperty(ref _batchModeDirect, value)) { OnPropertyChanged(nameof(BatchModeReplace)); UpdateBatchPreview(); } }
        }

        public bool BatchModeReplace
        {
            get => !_batchModeDirect;
            set => BatchModeDirect = !value;
        }

        private string _batchNewValue = "";
        public string BatchNewValue
        {
            get => _batchNewValue;
            set { if (SetProperty(ref _batchNewValue, value)) UpdateBatchPreview(); }
        }

        private string _batchFindText = "";
        public string BatchFindText
        {
            get => _batchFindText;
            set { if (SetProperty(ref _batchFindText, value)) UpdateBatchPreview(); }
        }

        private string _batchReplaceText = "";
        public string BatchReplaceText
        {
            get => _batchReplaceText;
            set { if (SetProperty(ref _batchReplaceText, value)) UpdateBatchPreview(); }
        }

        private bool _batchMatchCase = false;
        public bool BatchMatchCase
        {
            get => _batchMatchCase;
            set { if (SetProperty(ref _batchMatchCase, value)) UpdateBatchPreview(); }
        }

        private string _batchPreview = "(Nhập giá trị để xem trước)";
        public string BatchPreview
        {
            get => _batchPreview;
            set => SetProperty(ref _batchPreview, value);
        }

        private string _batchStatus = "";
        public string BatchStatus
        {
            get => _batchStatus;
            set => SetProperty(ref _batchStatus, value);
        }

        // Internal data
        private readonly Dictionary<string, string> _sampleAttrValues = new(StringComparer.OrdinalIgnoreCase);

        // Commands
        public ICommand BatchPickSampleCommand { get; }
        public ICommand BatchClearBlockNameCommand { get; }
        public ICommand BatchApplyCommand { get; }
        public ICommand BatchPickOneByOneCommand { get; }

        private void UpdateBatchSampleVal()
        {
            string tag = _batchAttrTag?.Trim() ?? "";
            if (_sampleAttrValues.TryGetValue(tag, out var val))
            {
                BatchSampleVal = val;
            }
        }

        private void UpdateBatchPreview()
        {
            string tag = _batchAttrTag?.Trim() ?? "";
            string curVal = _batchSampleVal;
            if (string.IsNullOrEmpty(curVal) || curVal == "(Chưa chọn mẫu)") curVal = "14";

            if (string.IsNullOrEmpty(tag))
            {
                BatchPreview = "(Vui lòng chọn hoặc nhập Attribute TAG)";
                return;
            }

            if (_batchModeDirect)
            {
                BatchPreview = $"{tag}: \"{curVal}\"  ➔  \"{_batchNewValue}\"";
            }
            else
            {
                string previewVal = ReplaceSubstring(curVal, _batchFindText, _batchReplaceText, _batchMatchCase);
                BatchPreview = $"{tag}: \"{curVal}\"  ➔  \"{previewVal}\"";
            }
        }

        private void ExecuteBatchPickSample()
        {
            if (!CheckCadAvailable()) return;
            var result = _cadService.PickBlock(OwnerWindow!, "Click chọn một Block mẫu trên CAD (ví dụ: COIL): ");
            if (result == null || result.IsText) return;

            _sampleAttrValues.Clear();
            foreach (var kv in result.AllAttributes)
                _sampleAttrValues[kv.Key] = kv.Value;

            BatchBlockName = result.BlockName;
            BatchTagList = result.AllAttributes.Keys.ToList();
            BatchSampleInfo = $"Block: '{result.BlockName}' ({_sampleAttrValues.Count} TAGs)";

            if (!string.IsNullOrEmpty(result.ClosestTag))
            {
                BatchAttrTag = result.ClosestTag;
                if (_sampleAttrValues.TryGetValue(result.ClosestTag, out var val))
                {
                    BatchSampleVal = val;
                    if (string.IsNullOrEmpty(_batchFindText)) BatchFindText = val;
                }
            }
            else if (BatchTagList.Count > 0)
            {
                BatchAttrTag = BatchTagList[0];
            }

            UpdateBatchPreview();
        }

        private void ExecuteBatchApply()
        {
            if (!CheckCadAvailable()) return;

            if (string.IsNullOrWhiteSpace(_batchAttrTag))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập Attribute TAG cần đổi (ví dụ: TERM01, TERM02, MODEL, NAME...).", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_batchModeDirect && string.IsNullOrEmpty(_batchFindText))
            {
                MessageBox.Show("Vui lòng nhập chuỗi cần tìm trong ô 'Tìm chuỗi'.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _cadService.BatchEditAttributes(
                OwnerWindow!,
                _batchBlockName, _batchAttrTag,
                _batchModeDirect, _batchNewValue,
                _batchFindText, _batchReplaceText, _batchMatchCase);

            if (result.Count > 0)
                BatchStatus = $"✅ Đã đổi thành công {result.Count} Block vào CAD! (TAG: '{_batchAttrTag}')";
            else
                BatchStatus = "⚠️ Không tìm thấy Block nào phù hợp trong vùng chọn.";
        }

        private void ExecuteBatchPickOneByOne()
        {
            if (!CheckCadAvailable()) return;

            if (string.IsNullOrWhiteSpace(_batchAttrTag))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập Attribute TAG cần đổi (ví dụ: TERM01, TERM02...).", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _cadService.BatchEditOneByOne(
                OwnerWindow!,
                _batchAttrTag,
                _batchModeDirect, _batchNewValue,
                _batchFindText, _batchReplaceText, _batchMatchCase);

            if (result.Count > 0)
                BatchStatus = $"✅ Đã đổi {result.Count} Block vào CAD! (TAG: '{_batchAttrTag}')";
        }

        #endregion

        #region ═══════════════ TAB 4: Rotate Attribute ═══════════════

        private string _rotateBlockName = "";
        public string RotateBlockName
        {
            get => _rotateBlockName;
            set
            {
                if (SetProperty(ref _rotateBlockName, value))
                {
                    UpdateRotatePreview();
                    if (!string.IsNullOrWhiteSpace(value) && _cadService.IsAvailable)
                    {
                        var tags = _cadService.GetBlockAttributeTags(value.Trim());
                        if (tags != null && tags.Count > 0)
                        {
                            RotateTagList = new ObservableCollection<string>(tags);
                            RotateSampleInfo = $"Block: '{value.Trim()}' ({tags.Count} TAGs)";
                            if (string.IsNullOrEmpty(_rotateAttrTag) || !_rotateTagList.Contains(_rotateAttrTag, StringComparer.OrdinalIgnoreCase))
                            {
                                RotateAttrTag = tags[0];
                            }
                        }
                    }
                }
            }
        }

        private string _rotateSampleInfo = "(Click để tự nạp Block, TAG & góc)";
        public string RotateSampleInfo
        {
            get => _rotateSampleInfo;
            set => SetProperty(ref _rotateSampleInfo, value);
        }

        private string _rotateAttrTag = "";
        public string RotateAttrTag
        {
            get => _rotateAttrTag;
            set
            {
                if (SetProperty(ref _rotateAttrTag, value))
                {
                    UpdateRotateSampleAngle();
                    UpdateRotatePreview();
                }
            }
        }

        private ObservableCollection<string> _rotateTagList = new();
        public ObservableCollection<string> RotateTagList
        {
            get => _rotateTagList;
            set => SetProperty(ref _rotateTagList, value);
        }

        private string _rotateSampleAngleText = "(Chưa chọn mẫu)";
        public string RotateSampleAngleText
        {
            get => _rotateSampleAngleText;
            set => SetProperty(ref _rotateSampleAngleText, value);
        }

        private double _rotateAngle = 90.0;
        public string RotateAngleText
        {
            get => _rotateAngle.ToString("0.##");
            set
            {
                if (double.TryParse(value, out double v))
                {
                    _rotateAngle = v;
                    OnPropertyChanged();
                    UpdateRotatePreview();
                }
            }
        }

        private bool _rotateModeAbsolute = true;
        public bool RotateModeAbsolute
        {
            get => _rotateModeAbsolute;
            set
            {
                if (SetProperty(ref _rotateModeAbsolute, value))
                {
                    OnPropertyChanged(nameof(RotateModeRelative));
                    UpdateRotatePreview();
                }
            }
        }

        public bool RotateModeRelative
        {
            get => !_rotateModeAbsolute;
            set => RotateModeAbsolute = !value;
        }

        private bool _rotateKeepCenter = true;
        public bool RotateKeepCenter
        {
            get => _rotateKeepCenter;
            set => SetProperty(ref _rotateKeepCenter, value);
        }

        private double _rotateOffset = 2.0;
        public string RotateOffset
        {
            get => _rotateOffset.ToString("0.##");
            set
            {
                if (double.TryParse(value, out double v))
                {
                    _rotateOffset = v;
                    OnPropertyChanged();
                    UpdateRotatePreview();
                }
            }
        }

        private bool _rotateIncludeText = false;
        public bool RotateIncludeText
        {
            get => _rotateIncludeText;
            set => SetProperty(ref _rotateIncludeText, value);
        }

        private string _rotatePreview = "(Chọn hoặc nhập TAG để xem trước)";
        public string RotatePreview
        {
            get => _rotatePreview;
            set => SetProperty(ref _rotatePreview, value);
        }

        private string _rotateStatus = "";
        public string RotateStatus
        {
            get => _rotateStatus;
            set => SetProperty(ref _rotateStatus, value);
        }

        // Internal tag rotations from picked sample
        private readonly Dictionary<string, double> _sampleAttrRotations = new(StringComparer.OrdinalIgnoreCase);

        // Commands
        public ICommand RotatePickSampleCommand { get; }
        public ICommand RotateClearBlockNameCommand { get; }
        public ICommand RotatePickTagCommand { get; }
        public ICommand RotatePickAngleCommand { get; }
        public ICommand RotateSetAnglePresetCommand { get; }
        public ICommand RotateApplyCommand { get; }
        public ICommand RotatePickOneByOneCommand { get; }

        private void ExecuteRotateSetPreset(string? param)
        {
            if (double.TryParse(param, out double angle))
            {
                _rotateAngle = angle;
                OnPropertyChanged(nameof(RotateAngleText));
                UpdateRotatePreview();
            }
        }

        private void UpdateRotateSampleAngle()
        {
            string tag = _rotateAttrTag?.Trim() ?? "";
            if (_sampleAttrRotations.TryGetValue(tag, out double angle))
            {
                RotateSampleAngleText = $"{angle:0.##}° ({GetAngleDescription(angle)})";
            }
        }

        private void UpdateRotatePreview()
        {
            string tag = _rotateAttrTag?.Trim() ?? "";
            if (string.IsNullOrEmpty(tag))
            {
                RotatePreview = "(Vui lòng chọn hoặc nhập Attribute TAG)";
                return;
            }

            string desc = GetAngleDescription(_rotateAngle);
            string offsetDesc = Math.Abs(_rotateOffset) > 1e-4 ? $" [Hở: {_rotateOffset:0.##}]" : "";
            if (_rotateModeAbsolute)
            {
                RotatePreview = $"TAG: '{tag}'  ➔  Góc mới: {_rotateAngle:0.##}° ({desc}){offsetDesc}";
            }
            else
            {
                RotatePreview = $"TAG: '{tag}'  ➔  Xoay thêm: +{_rotateAngle:0.##}°{offsetDesc}";
            }
        }

        private static string GetAngleDescription(double angle)
        {
            double norm = (angle % 360.0 + 360.0) % 360.0;
            if (Math.Abs(norm - 90.0) < 0.1) return "Dọc - Hướng lên";
            if (Math.Abs(norm - 270.0) < 0.1) return "Dọc - Hướng xuống";
            if (Math.Abs(norm - 0.0) < 0.1 || Math.Abs(norm - 360.0) < 0.1) return "Ngang chuẩn";
            if (Math.Abs(norm - 180.0) < 0.1) return "Ngang lật";
            return $"Nghiêng {norm:0.#}°";
        }

        private void ExecuteRotatePickSample()
        {
            if (!CheckCadAvailable()) return;
            var result = _cadService.PickBlock(OwnerWindow!, "Click chọn một Block mẫu trên CAD (ví dụ: Tiếp điểm, Cảm biến...): ");
            if (result == null) return;

            if (result.IsText)
            {
                RotateSampleInfo = $"Text: '{result.TextContent}' (Góc: {result.RotationDegrees:0.##}°)";
                RotateSampleAngleText = $"{result.RotationDegrees:0.##}° ({GetAngleDescription(result.RotationDegrees)})";
                if (!string.IsNullOrEmpty(result.TextContent))
                {
                    RotateAttrTag = result.TextContent;
                }
                RotateIncludeText = true;
                UpdateRotatePreview();
                return;
            }

            _sampleAttrRotations.Clear();
            foreach (var kv in result.AttributeRotations)
                _sampleAttrRotations[kv.Key] = kv.Value;

            RotateBlockName = result.BlockName;
            RotateTagList = new ObservableCollection<string>(result.AllAttributes.Keys);
            RotateSampleInfo = $"Block: '{result.BlockName}' ({RotateTagList.Count} TAGs)";

            if (!string.IsNullOrEmpty(result.ClosestTag))
            {
                RotateAttrTag = result.ClosestTag;
            }
            else if (RotateTagList.Count > 0)
            {
                RotateAttrTag = RotateTagList[0];
            }

            UpdateRotateSampleAngle();
            UpdateRotatePreview();
        }

        private void ExecuteRotatePickTag()
        {
            if (!CheckCadAvailable()) return;
            var result = _cadService.PickBlock(OwnerWindow!, "Click vào Attribute trên bản vẽ để lấy TAG cần xoay: ");
            if (result == null) return;

            if (!result.IsText)
            {
                _sampleAttrRotations.Clear();
                foreach (var kv in result.AttributeRotations)
                    _sampleAttrRotations[kv.Key] = kv.Value;

                RotateBlockName = result.BlockName;
                RotateTagList = new ObservableCollection<string>(result.AllAttributes.Keys);
                RotateSampleInfo = $"Block: '{result.BlockName}' ({RotateTagList.Count} TAGs)";

                if (!string.IsNullOrEmpty(result.ClosestTag))
                {
                    RotateAttrTag = result.ClosestTag;
                }
                else if (RotateTagList.Count > 0)
                {
                    RotateAttrTag = RotateTagList[0];
                }

                UpdateRotateSampleAngle();
                UpdateRotatePreview();
            }
            else if (!string.IsNullOrEmpty(result.TextContent))
            {
                RotateAttrTag = result.TextContent;
                RotateIncludeText = true;
                RotateSampleAngleText = $"{result.RotationDegrees:0.##}° ({GetAngleDescription(result.RotationDegrees)})";
                UpdateRotatePreview();
            }
        }

        private void ExecuteRotatePickAngle()
        {
            if (!CheckCadAvailable()) return;
            var angle = _cadService.PickAngle(OwnerWindow!);
            if (angle.HasValue)
            {
                _rotateAngle = angle.Value;
                OnPropertyChanged(nameof(RotateAngleText));
                UpdateRotatePreview();
                RotateStatus = $"Đã lấy góc từ CAD: {angle.Value:0.##}° ({GetAngleDescription(angle.Value)})";
            }
        }

        private void ExecuteRotateApply()
        {
            if (!CheckCadAvailable()) return;

            if (string.IsNullOrWhiteSpace(_rotateAttrTag))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập Attribute TAG cần xoay (ví dụ: NAME, TERM01, CYL1_OUT...).", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _cadService.RotateAttributes(
                OwnerWindow!,
                _rotateBlockName,
                _rotateAttrTag,
                _rotateAngle,
                RotateModeRelative,
                _rotateIncludeText,
                _rotateKeepCenter,
                _rotateOffset);

            if (result.Count > 0)
            {
                string desc = GetAngleDescription(_rotateAngle);
                RotateStatus = $"✅ Đã xoay thành công {result.Count} đối tượng sang {desc} ({_rotateAngle:0.##}°)!";
            }
            else
            {
                RotateStatus = "⚠️ Không tìm thấy đối tượng nào phù hợp trong vùng chọn.";
            }
        }

        private void ExecuteRotatePickOneByOne()
        {
            if (!CheckCadAvailable()) return;

            if (string.IsNullOrWhiteSpace(_rotateAttrTag))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập Attribute TAG cần xoay.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _cadService.RotateOneByOne(
                OwnerWindow!,
                _rotateAttrTag,
                _rotateAngle,
                RotateModeRelative,
                _rotateKeepCenter,
                _rotateOffset);

            if (result.Count > 0)
            {
                RotateStatus = $"✅ Đã xoay {result.Count} đối tượng sang {_rotateAngle:0.##}°!";
            }
        }

        #endregion

        #region ═══════════════ Footer & Tab ═══════════════

        private int _selectedTabIndex = 1;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { if (SetProperty(ref _selectedTabIndex, value)) UpdateFooterTip(); }
        }

        private string _footerTip = "💡 Mẹo: Nhấn 'Chọn TAG' rồi click vào đối tượng trên CAD để tự điền tên Block và TAG.";
        public string FooterTip
        {
            get => _footerTip;
            set => SetProperty(ref _footerTip, value);
        }

        public ICommand CloseCommand { get; }

        private void UpdateFooterTip()
        {
            FooterTip = _selectedTabIndex switch
            {
                0 => "💡 Mẹo: Nhấn 'Chọn TAG' rồi click vào khung tên để tự nhận diện Block và TAG số trang (A00).",
                2 => "💡 Mẹo: Nhấn 'Chọn Block mẫu trên CAD' rồi click vào block (ví dụ: COIL) để tự nạp danh sách TAG (TERM01, TERM02...).",
                3 => "💡 Mẹo: Nhấn '⬆️ 90°' để xoay dọc thẳng đứng, rồi quét chọn các khối trên CAD để xoay hàng loạt.",
                _ => "💡 Mẹo: Nhấn 'Chọn TAG' rồi click vào đối tượng trên CAD để tự điền tên Block và TAG."
            };
        }

        private void ExecuteClose()
        {
            OwnerWindow?.Close();
        }

        #endregion

        #region ═══════════════ Shared Utilities (static, reused by Service) ═══════════════

        /// <summary>
        /// Sắp xếp danh sách đối tượng theo hướng và dung sai hàng/cột.
        /// Static vì được dùng bởi cả ViewModel (preview) và Service (thực thi CAD).
        /// </summary>
        public static List<(Entity ent, Point3d pos)> SortEntitiesByDirection(List<(Entity ent, Point3d pos)> items, int dirIndex)
        {
            if (items.Count <= 1) return items;
            const double tolerance = 20.0;

            return dirIndex switch
            {
                1 => items.GroupBy(item => Math.Round(item.pos.Y / tolerance))
                          .OrderBy(grp => grp.Key)
                          .SelectMany(grp => grp.OrderBy(item => item.pos.X)).ToList(),

                2 => items.GroupBy(item => Math.Round(item.pos.X / tolerance))
                          .OrderBy(grp => grp.Key)
                          .SelectMany(grp => grp.OrderByDescending(item => item.pos.Y)).ToList(),

                3 => items.GroupBy(item => Math.Round(item.pos.X / tolerance))
                          .OrderBy(grp => grp.Key)
                          .SelectMany(grp => grp.OrderBy(item => item.pos.Y)).ToList(),

                _ => items.GroupBy(item => Math.Round(item.pos.Y / tolerance))
                          .OrderByDescending(grp => grp.Key)
                          .SelectMany(grp => grp.OrderBy(item => item.pos.X)).ToList(),
            };
        }

        /// <summary>
        /// Định dạng số thứ tự theo kiểu chọn: 1,2,3 | A,B,C | I,II,III
        /// Static vì được dùng bởi cả ViewModel (preview) và Service (thực thi CAD).
        /// </summary>
        public static string FormatNumber(int number, int styleIndex, bool twoDigits)
        {
            return styleIndex switch
            {
                1 => number <= 26 ? ((char)('A' + number - 1)).ToString() : number.ToString(),
                2 => ToRoman(number),
                _ => twoDigits ? number.ToString("D2") : number.ToString(),
            };
        }

        /// <summary>
        /// Chuyển đổi số nguyên sang số La Mã (I, II, III, IV, V, ...)
        /// </summary>
        public static string ToRoman(int number)
        {
            if (number <= 0 || number > 3999) return number.ToString();
            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            return thousands[number / 1000] + hundreds[(number % 1000) / 100] + tens[(number % 100) / 10] + ones[number % 10];
        }

        private static string ReplaceSubstring(string input, string find, string replaceWith, bool matchCase)
        {
            if (string.IsNullOrEmpty(find)) return input;
            if (matchCase) return input.Replace(find, replaceWith);
            return System.Text.RegularExpressions.Regex.Replace(
                input,
                System.Text.RegularExpressions.Regex.Escape(find),
                replaceWith,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private bool CheckCadAvailable()
        {
            if (!_cadService.IsAvailable)
            {
                MessageBox.Show(
                    "Tính năng này yêu cầu AutoCAD đang mở bản vẽ.\nHiện tại bạn đang ở chế độ Test Runner (chạy thử giao diện độc lập).",
                    "Chế độ Test Runner", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        #endregion
    }
}
