# Kiến Trúc Hệ Thống & Luồng Xử Lý CadElectricalToolkit

Tài liệu này mô tả chi tiết kiến trúc kỹ thuật, mô hình dữ liệu và các luồng xử lý (Data Flow & Execution Pipelines) của plugin **CadElectricalToolkit**.

---

## 1. Sơ đồ Kiến trúc Tổng thể

```
   +-----------------------------------------------------------------------------------+
   |                                 GIAO DIỆN NGƯỜI DÙNG                              |
   |                                                                                   |
   |   [Command Line Prompts]         [WPF Views - XAML Dark Glassmorphism]            |
   |   (Editor.GetSelection, etc.)    (AutoNumberingWindow, ToolInfoWindow)            |
   +----------------+-----------------------------------+------------------------------+
                    |                                   | Data Binding & ICommand
                    v                                   v
   +--------------------------------+   +----------------------------------------------+
   |      Lớp Điều khiển Lệnh       |   |                 Lớp ViewModel                |
   |    (Commands / Controllers)    |   |     (AutoNumberingViewModel, ToolInfoVM)     |
   |  CrossRefCommands,             |   | - State management, Live preview formatting  |
   |  DraftingCommands, etc.        |   | - Kế thừa ViewModelBase (INotifyProperty)    |
   +----------------+---------------+   +-----------------------+----------------------+
                    |                                           | Gọi thực thi CAD
                    |                                           v
                    |                   +----------------------------------------------+
                    |                   |            Lớp Dịch vụ (Service Layer)       |
                    |                   |     (ICadInteractionService / CadService)    |
                    |                   | - Document locking & StartUserInteraction    |
                    |                   | - Selection, Picking & Batch Attribute Edits |
                    |                   +-----------------------+----------------------+
                    |                                           |
                    +--------------------+----------------------+
                                         |
                                         v
   +-------------------------------------+---------------------------------------------+
   |                           Lớp Tương tác CAD (CadAccess)                           |
   |  CadDatabaseHelper   |   SheetFrameHelper   |   AttributeHelper   |  TableHelper  |
   |  (RunTransaction)    |   (Grid Coordinate)  |   (Safe Upgrades)   |  (Native Table|
   +-------------------------------------+---------------------------------------------+
                                         |
                                         v
   +-----------------------------------------------------------------------------------+
   |                             AutoCAD Database / ARX Runtime                        |
   |                  (Database, ModelSpace, Transactions, BlockReferences)            |
   +-----------------------------------------------------------------------------------+
```

---

## 2. Chi tiết Luồng Xử Lý Chính

### 2.1. Luồng Lệnh `TCCTB`: Đánh lại địa chỉ cuộn coil cho tiếp điểm với số lượng lớn

1. **Thu thập Đầu vào:**
   - Người dùng gõ lệnh `TCCTB`.
   - Plugin hiển thị prompt: `Quét chọn các block Cuộn coil & Tiếp điểm (nhấn ENTER để xử lý TOÀN BỘ bản vẽ)`.
   - Nếu người dùng quét chọn $\rightarrow$ chỉ xử lý tập block đó.
   - Nếu người dùng nhấn ENTER $\rightarrow$ tự động quét tất cả `BlockReference` trong `ModelSpace`.

2. **Nhận diện Khung tên (`SheetFrame`):**
   - Gọi `SheetFrameHelper.ScanAllFramesAuto(db, tr)`.
   - Nhận diện các block có tên trong danh sách (`Frame-a4`, `Frame-a3`, `KHUNG_TEN`,...) hoặc có tag số trang (`A00`, `SHEET_NO`,...).
   - Lấy extents an toàn (`GeometricExtents` hoặc `Bounds`).
   - Sắp xếp thứ tự các khung theo số trang.

3. **Phân loại Block (Cuộn Coil vs Tiếp điểm vs Khung 14 Chân):**
   - Duyệt qua danh sách block:
     - **Tiếp điểm (Contact):** Block có chứa attribute tag `ADDRESS_COIL` (hoặc các alias `ADDRESS_C`, `ADDRESS`, `REF`).
     - **Bảng thống kê (`KHUNG 14 CHAN`):** Block có tên khớp `ElectricalConfig.BlockRelayFrame`. Được lưu làm fallback nếu thiết bị chưa có cuộn coil riêng.
     - **Cuộn hút (Coil):** Block có attribute `NAME` và **không có** attribute `ADDRESS_COIL`.

4. **Tính toán Tọa độ Lưới (`GetAddress`):**
   - Xác định khung tên chứa cuộn hút qua `SheetFrameHelper.FindFrameContaining(frames, coilPos)`.
   - Áp dụng các tỉ lệ biên lề trong `ElectricalConfig`:
     $$\text{GridTopY} = \text{MaxPoint.Y} - (\text{Height} \times \text{FrameTopMarginRatio})$$
     $$\text{GridHeight} = \text{Height} \times (1 - \text{FrameTopMarginRatio} - \text{FrameBottomMarginRatio})$$
     $$\text{RowHeight} = \frac{\text{GridHeight}}{\text{FrameRows}}$$
     $$\text{RowIndex} = \lfloor \frac{\text{GridTopY} - y}{\text{RowHeight}} \rfloor \quad (\text{kẹp trong } 0 \dots \text{FrameRows}-1)$$
     $$\text{Row} = \text{RowIndex} + 1$$
   - Tương tự với cột: trừ lề trái (gáy đóng) và lề phải, chia đều 8 cột A..H.
   - Định dạng địa chỉ: `{SheetNo}-{Row}{Col}` (ví dụ: `01-4A`, `01-5B`).

5. **Ghi Dữ liệu vào Tiếp điểm:**
   - Với mỗi tiếp điểm của thiết bị:
     - Tìm toạ độ cuộn coil từ `coilsMap[deviceName]`.
     - Gọi `contactRef.SetAttributeValue(tr, coilAddress, TagAddressContactAliases)`.
     - Bên trong `SetAttributeValue`: Mở `attRef` bằng `OpenMode.ForRead`, kiểm tra tag, sau đó gọi `attRef.UpgradeOpen()` trước khi gán `attRef.TextString = coilAddress`. Tránh hoàn toàn lỗi `eWasOpenForRead`.

6. **Báo cáo Kết quả:**
   - In chi tiết ra command line danh sách từng thiết bị, cặp chân và toạ độ cuộn coil đã gán.

---

### 2.2. Luồng Lệnh `TKCRL`: Thống kê chân số thiết bị & Điền vào block KHUNG 14 CHÂN

1. **Bước 1: Quét chọn cuộn hút (Coil):**
   - Người dùng quét chọn một hoặc nhiều cuộn hút trên bản vẽ.
   - Plugin đọc `NAME`, lưu toạ độ `blkRef.Position` vào danh sách `deviceInfos`.
2. **Bước 2: Quét chọn khung tên (Frame):**
   - Người dùng quét chọn các khung tên A4/A3 trên bản vẽ để xác định phạm vi toạ độ.
3. **Bước 3: Tự động xử lý & Đồng bộ:**
   - **Tự động gán `ADDRESS_COIL`** cho tất cả tiếp điểm của các cuộn coil đã chọn.
   - Quét toàn bộ tiếp điểm mang cùng `NAME` trên bản vẽ, trích xuất cặp chân (`TERM01-TERM02`), tính toạ độ lưới (`frame.GetAddress`).
4. **Bước 4: Cập nhật hoặc Chèn KHUNG 14 CHÂN mới:**
   - Nếu thiết bị đã có sẵn block `KHUNG 14 CHAN`: Tự động cập nhật `ADDRESS_1..4`.
   - Nếu chưa có: Kích hoạt `BlockPlacementJig`:
     - Khối block "ma" (ghost block) đi theo con trỏ chuột.
     - Người dùng nhấp chuột lần lượt tại vị trí mong muốn để đặt từng bảng rơ-le.

---

### 2.3. Luồng Lệnh `DSTTKBT`, `DSTT`, `DOIATT`: Đánh số thứ tự & Đổi thuộc tính qua WPF MVVM UI

Cả 3 lệnh đều tích hợp vào cửa sổ WPF hiện đại [`AutoNumberingWindow`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Views/AutoNumberingWindow.xaml) với 3 tab chức năng:

1. **Tab 1: STT Khung tên (`DSTTKBT`):**
   - Người dùng cài đặt: Tiền tố (Prefix), Hậu tố (Suffix), Số bắt đầu (From), Kiểu số (`1, 2, 3`, `A, B, C`, `I, II, III`), 2 chữ số (`01`), Hướng quét (4 hướng trực giao).
   - Chế độ trang: Ghi dạng trang hiện tại/tổng trang (`12/24`), Tag số trang (`A00`), Tag tổng trang (`TSHEET`), tự động đếm tổng số trang trong vùng chọn hoặc nhập tay.
   - Nhấn `⚡ QUÉT CHỌN & ÁP DỤNG`: Ẩn tạm thời dialog (`StartUserInteraction`), người dùng quét chọn các khung tên trên CAD, tự động sắp xếp tọa độ và ghi số trang trong một Transaction an toàn.

2. **Tab 2: STT Block Attribute & Text (`DSTT`):**
   - Đánh số tăng dần cho chân cầu đấu, rơ-le, thiết bị (ví dụ `DI.01`, `DI.02`... hoặc `TB1-1`, `TB1-2`...).
   - Hỗ trợ nút `🎯 Chọn TAG`: Click trực tiếp vào Block hoặc Text trên CAD để tự động nhận diện Block Name và Attribute TAG gần nhất.
   - Hỗ trợ 2 chế độ:
     - `⚡ Quét chọn hàng loạt`: Áp dụng theo thứ tự hướng quét đã chọn.
     - `👆 Click từng cái`: Click liên tiếp trên CAD, mỗi lần click tự động gán giá trị tăng dần và hiển thị thông báo ngay tại con trỏ.

3. **Tab 3: Đổi Attribute hàng loạt (`DOIATT` / `DATTR`):**
   - Nhấn `🎯 Chọn Block mẫu trên CAD`: Click vào một block bất kỳ (ví dụ: `COIL`), nạp toàn bộ danh sách TAG vào ComboBox (`TERM01`, `TERM02`, `NAME`, `MODEL`...) kèm giá trị hiện tại.
   - Hỗ trợ 2 chế độ đổi:
     - **Gán giá trị mới trực tiếp**: Ghi đè toàn bộ giá trị cho tag được chọn.
     - **Tìm & Thay thế chuỗi**: Tìm chuỗi con và thay thế bằng chuỗi mới (hỗ trợ tùy chọn phân biệt chữ hoa/thường).
   - Hỗ trợ cả quét chọn vùng hoặc click từng đối tượng.

---

## 3. Kiến Trúc WPF UI Hiện Đại & Mô Hình MVVM Chuẩn

Hệ thống UI được tái cấu trúc toàn diện theo mô hình **MVVM (Model-View-ViewModel)** chuẩn của ứng dụng chuyên nghiệp:

```
[View (XAML)]  <==== DataBinding / ICommand ====>  [ViewModel]  <==== Calls ====>  [CadInteractionService]  <==== Uses ====>  [AutoCAD Database / Editor]
```

### 3.1. Các Thành phần Kiến trúc

1. **Lớp Hạ tầng MVVM (`UI/Mvvm/`):**
   - [`ViewModelBase`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Mvvm/ViewModelBase.cs): Cài đặt `INotifyPropertyChanged` với phương thức tiện ích `SetProperty<T>()`, tự động kiểm tra giá trị và kích hoạt thông báo thay đổi giao diện.
   - [`RelayCommand`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Mvvm/RelayCommand.cs): Cài đặt `ICommand` hỗ trợ cả hành động không tham số và có tham số, tự động lắng nghe `CommandManager.RequerySuggested` để cập nhật trạng thái kích hoạt nút bấm.

2. **Lớp Dịch vụ Tương tác CAD (`UI/Services/`):**
   - [`ICadInteractionService`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Services/ICadInteractionService.cs): Định nghĩa hợp đồng trừu tượng cho tất cả tương tác người dùng trên bản vẽ:
     - `PickBlock(...)`: Chọn đối tượng đơn lẻ và trích xuất thông tin tag/text.
     - `NumberFrames(...)`: Quét và đánh số khung tên bản vẽ.
     - `NumberBlockAttributes(...)`: Đánh số hàng loạt block attribute hoặc text.
     - `NumberOneByOne(...)`: Đánh số click từng đối tượng theo thứ tự.
     - `BatchEditAttributes(...)` / `BatchEditOneByOne(...)`: Đổi thuộc tính hàng loạt.
   - [`CadInteractionService`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Services/CadInteractionService.cs): Triển khai thực tế sử dụng `ed.StartUserInteraction(window)`, `doc.LockDocument()`, và `CadDatabaseHelper.RunTransaction(...)`. Xử lý an toàn khi người dùng hủy bỏ (Esc).

3. **Lớp ViewModel (`UI/ViewModels/`):**
   - [`AutoNumberingViewModel`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/ViewModels/AutoNumberingViewModel.cs): Quản lý toàn bộ dữ liệu nhập, tính toán xem trước (Live Previews) theo thời gian thực (ví dụ: xem trước số trang `01 /24`, chuỗi thay thế, số La Mã), và các Command thực thi.
   - [`ToolInfoViewModel`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/ViewModels/ToolInfoViewModel.cs): Quản lý tìm kiếm nhanh lệnh, lọc theo nhóm danh mục, thực thi lệnh trực tiếp (`SendStringToExecute`), và sao chép mã lệnh vào Clipboard.

4. **Lớp View (`UI/Views/`):**
   - [`AutoNumberingWindow.xaml`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Views/AutoNumberingWindow.xaml) & [`ToolInfoWindow.xaml`](file:///f:/1.Project/1.C_shard/C_AutoCad/src/CadElectricalToolkit/UI/Views/ToolInfoWindow.xaml):
     - Giao diện Dark Theme cao cấp (`#0F1117`, `#1A1D27`, `#2A2D3A`).
     - Hiệu ứng đổ bóng mờ Glassmorphism, bo góc hiện đại, viền phát sáng nhẹ khi hover/focus.
     - Code-behind mỏng tối đa (Thin Code-Behind): chỉ chịu trách nhiệm khởi tạo `InitializeComponent()` và gán `DataContext = ViewModel`.

### 3.2. Thành Phần Giao Diện Đặc Trưng (UI Design Elements)

1. **Custom Dark ComboBox (`ModernDarkComboBox`):**
   - Thay thế toàn bộ popup trắng mặc định của Windows bằng popup nền tối (`#161924`), viền bo góc tròn (`CornerRadius="6"`, `#2E364A`) kèm đổ bóng `DropShadowEffect`.
   - Các item (`ComboBoxItem`) có chiều cao chuẩn 30px, hiệu ứng hover xám xanh `#252D40`, chữ trắng sáng `#FFFFFF`, và khi được chọn viền xanh `#3B82F6` nổi bật.
   - Nút Chevron Arrow dạng vector nét thanh (`Stroke="#94A3B8"` chuyển `#38BDF8` khi hover).
   - Loại bỏ hoàn toàn viền nét đứt (focus dotted box) thô của Windows.

2. **Slim Minimalist ScrollBar (Thanh cuộn siêu mỏng):**
   - Độ rộng siêu mỏng **7px** (thay thế thanh cuộn 18px thô kệch mặc định).
   - Loại bỏ hoàn toàn 2 nút mũi tên lên/xuống ở hai đầu thanh cuộn.
   - Rãnh trượt trong suốt (`Transparent`), không chiếm không gian hiển thị.
   - Con nhộng trượt (`Thumb`) bo tròn mềm mại (`CornerRadius="3"`), màu `#333C52`, sáng dần khi rê chuột (`#64748B`) và chuyển màu accent (`#818CF8`) khi kéo thả.

3. **Công cụ Kiểm Thử UI Độc Lập (`CadElectricalToolkit.UiRunner`):**
   - Ứng dụng WPF độc lập cho phép xem trước, thử nghiệm và tinh chỉnh giao diện người dùng mà không cần mở AutoCAD hay tải assembly vào tiến trình CAD.

---

## 4. Bảng Tổng Hợp 28 Lệnh Điều Khiển Của Plugin

| Phân hệ | Lệnh CAD | Phương thức xử lý | Class điều khiển | Giao diện |
| :--- | :--- | :--- | :--- | :--- |
| **Tra cứu** | `ELECINFO` / `TT` | `ShowToolInfo()` | `AboutCommands` | WPF `ToolInfoWindow` |
| | `TAOBLOCKMAU` | `CreateSampleBlocksCommand()` | `AboutCommands` | Command Line / Console |
| **Đánh số** | `DSTTKBT` | `SheetNumberingWithUI()` | `CrossRefCommands` | WPF `AutoNumberingWindow` (Tab 1) |
| | `DSTT` | `AutoNumbering()` | `DraftingCommands` | WPF `AutoNumberingWindow` (Tab 2) |
| | `DOIATT` / `DATTR` | `BatchChangeAttribute()` | `DraftingCommands` | WPF `AutoNumberingWindow` (Tab 3) |
| **Relay CRL** | `TCCTB` | `CrossReferenceRelay()` | `CrossRefCommands` | Command Line / Console |
| | `TKCRL` | `RelayTableCommand()` | `CrossRefCommands` | Mouse Jig & Console |
| | `SYNCRL` | `SyncRelayAddressCommand()` | `CrossRefCommands` | Command Line / Console |
| | `KTCRL` | `CheckRelayErrorsCommand()` | `CrossRefCommands` | Command Line / Console |
| **Truy vết** | `TIMTRANG` / `TRACE` | `TraceLinkCommand()` | `TraceCommands` | Command Line / Interactive |
| | `XOALINK` | `ClearTraceLinksCommand()` | `TraceCommands` | Command Line |
| **Khung tên** | `TKDMBV` | `SheetIndexCommand()` | `SheetCommands` | AutoCAD Native Table |
| | `SYNREV` | `SyncRevisionCommand()` | `SheetCommands` | Command Line |
| | `SYNTF` | `SyncToFromCommand()` | `SheetCommands` | Command Line |
| | `GBV` | `GroupSheetsCommand()` | `SheetCommands` | Command Line |
| | `TBV` | `SplitSheetsCommand()` | `SheetCommands` | Command Line |
| **Dây & Cầu đấu** | `INSGEN` | `InsertWireNumberCommand()` | `WireNumberCommands` | Command Line & Jig |
| | `TKGEN` | `ExportWireNumberListCommand()` | `WireNumberCommands` | AutoCAD Table / CSV |
| | `SYNGEN` | `SyncWireNumberCommand()` | `WireNumberCommands` | Command Line |
| | `TKCD` | `ExportTerminalListCommand()` | `TerminalCommands` | AutoCAD Native Table |
| | `SYNCD` | `SyncTerminalNumbersCommand()` | `TerminalCommands` | Command Line |
| **PLC I/O** | `TKPLCIN` | `ExportPlcInputCommand()` | `PlcCommands` | AutoCAD Native Table |
| | `TKPLCOUT` | `ExportPlcOutputCommand()` | `PlcCommands` | AutoCAD Native Table |
| | `SYNPLC` | `SyncPlcAddressesCommand()` | `PlcCommands` | Command Line |
| **Soạn thảo** | `CDN` | `QuickDimensionSum()` | `DraftingCommands` | Clipboard / Console |
| | `GTD` | `AlignTextVertical()` | `DraftingCommands` | Command Line |
| | `GTN` | `AlignTextHorizontal()` | `DraftingCommands` | Command Line |
| | `CAD2EXCEL` | `ExportCadToExcelCommand()` | `BomCommands` | File Dialog / CSV |
| | `TKDMTB` | `ExportBomListCommand()` | `BomCommands` | AutoCAD Table / CSV |


