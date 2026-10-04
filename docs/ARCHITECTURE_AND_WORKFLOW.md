# Kiến Trúc Hệ Thống & Luồng Xử Lý CadElectricalToolkit

Tài liệu này mô tả chi tiết kiến trúc kỹ thuật, mô hình dữ liệu và các luồng xử lý (Data Flow & Execution Pipelines) của plugin **CadElectricalToolkit**.

---

## 1. Sơ đồ Kiến trúc Tổng thể

```
                 +--------------------------------------------+
                 |            Giao diện Người dùng            |
                 |  (Command Line / WinForms Modal Dialogs)   |
                 +---------------------+----------------------+
                                       |
                                       v
                 +--------------------------------------------+
                 |            Lớp Điều khiển Lệnh             |
                 |           (Commands / Controllers)         |
                 |  CrossRefCommands, SheetCommands, v.v.     |
                 +---------------------+----------------------+
                                       |
                   +-------------------+-------------------+
                   |                                       |
                   v                                       v
+--------------------------------------+ +--------------------------------------+
|            Core Config               | |           CadAccess Layer            |
|       (ElectricalConfig.cs)          | |  (CadDatabaseHelper, SheetFrameHelper|
|  - Tags, Blocks, Ratios, Margins     | |   AttributeHelper, SelectionHelper)  |
+--------------------------------------+ +------------------+-------------------+
                                                            |
                                                            v
                                         +--------------------------------------+
                                         |         AutoCAD Database / ARX       |
                                         |       (ModelSpace, Transactions,     |
                                         |        BlockReferences, Attributes)  |
                                         +--------------------------------------+
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

### 2.3. Luồng Lệnh `DSTTKBT`: Đánh số thứ tự khung tên bản vẽ qua UI

1. **Hiển thị Modal Form (`SheetNumberingForm`):**
   - Người dùng cài đặt: Tiền tố (Prefix), Hậu tố (Suffix), Số bắt đầu (StartNumber), Kiểu số (`1, 2, 3`, `01, 02`, `001`, `A, B, C`), Hướng quét (Trên xuống dưới, Dưới lên trên, Trái qua phải, Phải qua trái, hoặc theo thứ tự chọn).
   - Chế độ trang: Ghi dạng trang hiện tại/tổng trang (`12/24`), Tag số trang (`A00`), Tag tổng trang (`TSHEET`).
2. **Quét chọn & Sắp xếp:**
   - Người dùng quét chọn các block khung tên trên bản vẽ.
   - Plugin sắp xếp danh sách block theo hướng đã chọn.
3. **Ghi số trang:**
   - Ghi giá trị số trang vào tag `A00`.
   - Ghi tổng số trang vào tag `TSHEET`.
