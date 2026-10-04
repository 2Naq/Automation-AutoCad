using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Bảng hiển thị thông tin Plugin và danh sách các lệnh hỗ trợ
    /// </summary>
    public class ToolInfoForm : Form
    {
        public ToolInfoForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "THÔNG TIN TOOLs";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(380, 560);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // Header Title
            var lblTitle = new Label
            {
                Text = "DANH SÁCH LỆNH",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(16, 75, 140),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 35
            };

            // ListBox / RichTextBox for commands
            var txtCommands = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Dock = DockStyle.Fill,
                Text = string.Join(Environment.NewLine, new[]
                {
                    "- TKCRL : Tạo bảng thống kê chân số thiết bị.",
                    "- TKPLCIN : Tạo bảng địa chỉ DI-PLC.",
                    "- TKPLCOUT : Tạo bảng địa chỉ DO-PLC.",
                    "- TKCD : Thống kê cầu đấu.",
                    "- TKDMBV : Thống kê danh mục bản vẽ.",
                    "- TKDMTB : Thống kê danh mục thiết bị.",
                    "- TKGEN : Thống kê gen số.",
                    "- INSGEN : Tạo gen số thống kê.",
                    "- TCCTB : Đánh lại địa chỉ cuộn coil cho tiếp điểm (hàng loạt / tự động).",
                    "- SYNPLC : Đồng bộ địa chỉ DI/DO-PLC.",
                    "- SYNREV : Cập nhật số lần thay đổi của trang bản vẽ.",
                    "- SYNCRL : Đồng bộ địa chỉ chân số thiết bị & cuộn coil 2 chiều.",
                    "- SYNTF : Cập nhật địa chỉ mũi tên TO-FROM.",
                    "- SYNCD : Đồng bộ tên cầu đấu theo trang bản vẽ.",
                    "- SYNGEN : Đồng bộ tên gen số theo trang bản vẽ.",
                    "- KTCRL : Kiểm tra chân số thiết bị.",
                    "- DSTT : Đánh số thứ tự.",
                    "- DSTTKBT : Đánh số thứ tự khung tên (UI cài đặt, hỗ trợ trang/tổng trang).",
                    "- CAD2EXCEL : Xuất thông tin ATT, TEXT ra EXCEL.",
                    "- GTD : Gom text dọc.",
                    "- GTN : Gom text ngang.",
                    "- GBV : Ghép bản vẽ outline.",
                    "- TBV : Tách bản vẽ.",
                    "- CDN : Cộng Dim nhanh."
                })
            };

            // Bottom Panel for info & OK button
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                Padding = new Padding(10, 5, 10, 10)
            };

            var lblSeparator = new Label
            {
                Text = new string('-', 75),
                ForeColor = Color.DarkGray,
                Location = new Point(10, 0),
                Size = new Size(360, 15)
            };

            var lblVersion = new Label
            {
                Text = "Thông tin phiên bản : CadElectricalToolkit v1.0",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(10, 18),
                Size = new Size(360, 20)
            };

            var lblAuthor = new Label
            {
                Text = "Hỗ trợ & Phát triển : C# AutoCAD 2021+",
                Font = new Font("Segoe UI", 8.5F),
                Location = new Point(10, 38),
                Size = new Size(250, 20)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(75, 28),
                Location = new Point(290, 50),
                BackColor = Color.FromArgb(240, 240, 240),
                UseVisualStyleBackColor = true
            };
            btnOk.Click += (s, e) => Close();

            pnlBottom.Controls.Add(lblSeparator);
            pnlBottom.Controls.Add(lblVersion);
            pnlBottom.Controls.Add(lblAuthor);
            pnlBottom.Controls.Add(btnOk);

            var pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 0, 10, 0)
            };
            pnlCenter.Controls.Add(txtCommands);

            Controls.Add(pnlCenter);
            Controls.Add(lblTitle);
            Controls.Add(pnlBottom);

            AcceptButton = btnOk;
        }
    }
}
