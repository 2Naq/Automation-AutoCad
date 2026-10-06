<p align="center">
  <img width="18%" align="center" src="https://2naq.github.io/shareme/favicon.svg" alt="logo">

</p>
<h1 align="center">
  Automation notes
</h1>
<p align="center">
  AutoCad NET Framework . C#.
</p>

<div align="center">

[![GitHub Release](https://img.shields.io/github/v/release/2Naq/Automation-AutoCad?color=blue&label=T%E1%BA%A3i%20V%E1%BB%81%20(Release))](https://github.com/2Naq/Automation-AutoCad/releases)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![AutoCAD](https://img.shields.io/badge/AutoCAD-2021%2B-red?logo=autodesk&logoColor=white)](https://www.autodesk.com/products/autocad)
[![Language: C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Website](https://img.shields.io/badge/Website-Shareme-green.svg)](https://2naq.github.io/shareme/)

</div>

---

## 🚀 Tải về & Cài đặt nhanh (Không cần build)

👉 **[Bấm vào đây để tải bản mới nhất từ GitHub Releases](https://github.com/2Naq/Automation-AutoCad/releases)**

Có 2 cách sử dụng sau khi tải:

### Cách 1: Tự động nạp khi mở AutoCAD (Khuyên dùng)
1. Tải file `CadElectricalToolkit-bundle.zip` từ mục **Releases**.
2. Giải nén thư mục `CadElectricalToolkit.bundle` vào đường dẫn:
   ```text
   %APPDATA%\Autodesk\ApplicationPlugins\
   (Tương đương: C:\Users\<Tên_Bạn>\AppData\Roaming\Autodesk\ApplicationPlugins\)
   ```
3. Khởi động AutoCAD, plugin sẽ tự động kích hoạt. Gõ lệnh `TT` hoặc `ELECINFO` để bắt đầu.

### Cách 2: Nạp thủ công bằng lệnh NETLOAD
1. Tải file `CadElectricalToolkit.dll` từ mục **Releases** lưu vào thư mục bất kỳ.
2. Mở AutoCAD, gõ lệnh `NETLOAD` và chọn đến file `CadElectricalToolkit.dll`.
3. Gõ `ELECINFO` hoặc `TT` để xem menu lệnh.

---

## Related Documentation

- [Developer Guide](./.agents/AGENTS.md)
- [Architecture & Workflows](./docs/ARCHITECTURE_AND_WORKFLOW.md)

## License

[MIT](./LICENSE)
