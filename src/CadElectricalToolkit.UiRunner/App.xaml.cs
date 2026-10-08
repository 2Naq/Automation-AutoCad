using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CadElectricalToolkit.UiRunner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Tự động nạp các thư viện AcCoreMgd/AcDbMgd từ AutoCAD khi cần thiết
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            // Thiết lập tìm kiếm DLLs AutoCAD để tránh lỗi missing assembly khi chạy độc lập
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    var requestedName = new AssemblyName(args.Name).Name;
                    string[] searchDirs = new[]
                    {
                        @"C:\Program Files\Autodesk\AutoCAD 2021",
                        @"C:\Program Files\Autodesk\AutoCAD 2022",
                        @"C:\Program Files\Autodesk\AutoCAD 2023",
                        @"C:\Program Files\Autodesk\AutoCAD 2024",
                        @"C:\Program Files\Autodesk\AutoCAD 2025"
                    };

                    foreach (var dir in searchDirs)
                    {
                        if (Directory.Exists(dir))
                        {
                            string candidate = Path.Combine(dir, requestedName + ".dll");
                            if (File.Exists(candidate))
                            {
                                return Assembly.LoadFrom(candidate);
                            }
                        }
                    }
                }
                catch
                {
                    // Bỏ qua lỗi resolution
                }
                return null;
            };
        }
    }
}
