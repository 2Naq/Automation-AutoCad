using System;
using System.Drawing;
using System.Windows.Forms;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Bảng cài đặt đánh số thứ tự khung tên (Sheet Numbering)
    /// Hỗ trợ 2 tab:
    ///   1. STT khung tên (đánh số trang cho các frame)
    ///   2. STT block attribute (đánh số attribute bất kỳ)
    /// </summary>
    public class SheetNumberingForm : Form
    {
        #region Controls

        private TabControl tabControl = null!;
        private TabPage tabFrame = null!;
        private TabPage tabBlockAttr = null!;

        // Tab 1: STT khung tên
        private TextBox txtPrefix = null!;
        private ComboBox cboNumberStyle = null!;
        private TextBox txtSuffix = null!;
        private NumericUpDown nudFrom = null!;
        private CheckBox chkTwoDigits = null!;
        private Label lblPreview = null!;
        private TextBox txtBlockName = null!;
        private TextBox txtAttrTag = null!;
        private Button btnChooseTag = null!;
        private ComboBox cboDirection = null!;
        private Button btnSelectBlocks = null!;

        // Tab 1 - Chế độ trang (A00 + TSHEET)
        private CheckBox chkPageMode = null!;
        private CheckBox chkAutoTotal = null!;
        private NumericUpDown nudTotalPages = null!;
        private TextBox txtTotalPagesTag = null!;
        private TextBox txtTotalPagesPrefix = null!;
        private Label lblPagePreview = null!;

        // Tab 2: STT block attribute
        private TextBox txtAttrPrefix = null!;
        private ComboBox cboAttrNumberStyle = null!;
        private TextBox txtAttrSuffix = null!;
        private NumericUpDown nudAttrFrom = null!;
        private CheckBox chkAttrTwoDigits = null!;
        private Label lblAttrPreview = null!;
        private TextBox txtAttrBlockName = null!;
        private TextBox txtAttrTagName = null!;
        private Button btnAttrChooseTag = null!;
        private ComboBox cboAttrDirection = null!;
        private Button btnAttrSelectBlocks = null!;

        // OK / Cancel
        private Button btnOK = null!;
        private Button btnCancel = null!;

        #endregion

        #region Public Result Properties

        /// <summary>Tiền tố (Prefix)</summary>
        public string NumberPrefix => IsFrameTab ? txtPrefix.Text : txtAttrPrefix.Text;

        /// <summary>Hậu tố (Suffix)</summary>
        public string NumberSuffix => IsFrameTab ? txtSuffix.Text : txtAttrSuffix.Text;

        /// <summary>Số bắt đầu</summary>
        public int StartNumber => IsFrameTab ? (int)nudFrom.Value : (int)nudAttrFrom.Value;

        /// <summary>Sử dụng 2 chữ số (01, 02, ...)</summary>
        public bool UseTwoDigits => IsFrameTab ? chkTwoDigits.Checked : chkAttrTwoDigits.Checked;

        /// <summary>Kiểu đánh số (index: 0=1,2,3 | 1=A,B,C | 2=I,II,III)</summary>
        public int NumberStyleIndex => IsFrameTab ? cboNumberStyle.SelectedIndex : cboAttrNumberStyle.SelectedIndex;

        /// <summary>Tên Block mục tiêu</summary>
        public string TargetBlockName => IsFrameTab ? txtBlockName.Text : txtAttrBlockName.Text;

        /// <summary>Tag Attribute mục tiêu</summary>
        public string TargetAttrTag => IsFrameTab ? txtAttrTag.Text : txtAttrTagName.Text;

        /// <summary>Hướng đánh số (0=Trái→Phải|Trên→Dưới, 1=Trái→Phải|Dưới→Trên, 2=Trên→Dưới|Trái→Phải)</summary>
        public int DirectionIndex => IsFrameTab ? cboDirection.SelectedIndex : cboAttrDirection.SelectedIndex;

        /// <summary>Tab hiện tại là tab Frame (true) hay tab Block Attribute (false)</summary>
        public bool IsFrameTab => tabControl.SelectedIndex == 0;

        /// <summary>Có cập nhật tổng số trang (TSHEET) không</summary>
        public bool UsePageMode => chkPageMode.Checked;

        /// <summary>Tự động đếm tổng số trang theo số block đã chọn</summary>
        public bool AutoTotalPages => chkAutoTotal.Checked;

        /// <summary>Tổng số trang cố định (khi AutoTotalPages = false)</summary>
        public int TotalPages => (int)nudTotalPages.Value;

        /// <summary>Tag ghi tổng số trang (mặc định: TSHEET)</summary>
        public string TotalPagesAttrTag => txtTotalPagesTag.Text.Trim();

        /// <summary>Tiền tố tổng số trang (mặc định: "/")</summary>
        public string TotalPagesPrefix => txtTotalPagesPrefix.Text;

        /// <summary>Người dùng đã nhấn "Chọn blocks" chưa</summary>
        public bool UserRequestedSelectBlocks { get; private set; }

        /// <summary>Người dùng đã nhấn "Chọn TAG" chưa</summary>
        public bool UserRequestedChooseTag { get; private set; }

        #endregion

        public SheetNumberingForm()
        {
            InitializeComponent();
            UpdatePreview();
            UpdatePagePreview();
        }

        private void InitializeComponent()
        {
            Text = "ĐÁNH SỐ THỨ TỰ";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 500);
            BackColor = Color.FromArgb(240, 240, 240);
            Font = new Font("Segoe UI", 9F);

            // ─── Tab Control ───
            tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(410, 360),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            // ═══════════════════════════════════════════
            // TAB 1: STT khung tên
            // ═══════════════════════════════════════════
            tabFrame = new TabPage("STT khung tên") { BackColor = Color.White, Padding = new Padding(10) };

            // ── Nhóm: Quy tắc đánh số ──
            var grpRule = new GroupBox
            {
                Text = "Quy tắc đánh số thứ tự",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0),
                Location = new Point(10, 5),
                Size = new Size(370, 145)
            };

            // Hàng 1: Prefix - Kiểu - Suffix
            var lblPrefixH = new Label { Text = "Prefix", Location = new Point(15, 22), Size = new Size(90, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };
            var lblStyleH = new Label { Text = "Kiểu đánh số", Location = new Point(120, 22), Size = new Size(120, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };
            var lblSuffixH = new Label { Text = "Suffix", Location = new Point(265, 22), Size = new Size(90, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };

            txtPrefix = new TextBox { Location = new Point(15, 42), Size = new Size(90, 23), Font = new Font("Segoe UI", 9F) };
            cboNumberStyle = new ComboBox
            {
                Location = new Point(120, 42), Size = new Size(130, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cboNumberStyle.Items.AddRange(new object[] { "1,2,3,...", "A,B,C,...", "I,II,III,..." });
            cboNumberStyle.SelectedIndex = 0;

            txtSuffix = new TextBox { Location = new Point(265, 42), Size = new Size(90, 23), Font = new Font("Segoe UI", 9F) };

            // Hàng 2: From + Hai chữ số
            var lblFrom = new Label { Text = "From:", Location = new Point(15, 75), Size = new Size(40, 20), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black };
            nudFrom = new NumericUpDown { Location = new Point(60, 73), Size = new Size(60, 23), Minimum = 1, Maximum = 9999, Value = 1, Font = new Font("Segoe UI", 9F) };
            chkTwoDigits = new CheckBox { Text = "Sử dụng hai chữ số", Location = new Point(140, 74), Size = new Size(200, 22), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black, Checked = true };

            // Hàng 3: Preview
            var lblPreviewLabel = new Label { Text = "Preview:", Location = new Point(15, 105), Size = new Size(60, 20), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black };
            lblPreview = new Label
            {
                Text = "01",
                Location = new Point(80, 103),
                Size = new Size(270, 22),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };

            grpRule.Controls.AddRange(new Control[] {
                lblPrefixH, lblStyleH, lblSuffixH,
                txtPrefix, cboNumberStyle, txtSuffix,
                lblFrom, nudFrom, chkTwoDigits,
                lblPreviewLabel, lblPreview
            });

            // ── Cài đặt tổng số trang (TSHEET) ──
            var grpPage = new GroupBox
            {
                Text = "Cài đặt tổng số trang (TSHEET)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 0),
                Location = new Point(10, 153),
                Size = new Size(370, 95)
            };

            chkPageMode = new CheckBox
            {
                Text = "Cập nhật tổng trang (TSHEET)",
                Location = new Point(12, 19),
                Size = new Size(185, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 0),
                Checked = true
            };

            var lblTotalTag = new Label { Text = "Tag:", Location = new Point(202, 20), Size = new Size(30, 18), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Black };
            txtTotalPagesTag = new TextBox
            {
                Text = "TSHEET",
                Location = new Point(232, 18),
                Size = new Size(58, 22),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 0)
            };

            var lblPrefix = new Label { Text = "Ký tự:", Location = new Point(295, 20), Size = new Size(36, 18), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Black };
            txtTotalPagesPrefix = new TextBox
            {
                Text = "/",
                Location = new Point(331, 18),
                Size = new Size(26, 22),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            chkAutoTotal = new CheckBox
            {
                Text = "Tự tính theo số khung chọn",
                Location = new Point(12, 44),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Black,
                Checked = true
            };

            var lblTotal = new Label { Text = "Cố định:", Location = new Point(202, 45), Size = new Size(50, 18), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Black };
            nudTotalPages = new NumericUpDown
            {
                Location = new Point(255, 43),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 9999,
                Value = 3,
                Font = new Font("Segoe UI", 8.5F),
                Enabled = false
            };

            lblPagePreview = new Label
            {
                Text = "",
                Location = new Point(12, 69),
                Size = new Size(345, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(0, 100, 0)
            };

            grpPage.Controls.AddRange(new Control[] {
                chkPageMode, lblTotalTag, txtTotalPagesTag, lblPrefix, txtTotalPagesPrefix,
                chkAutoTotal, lblTotal, nudTotalPages, lblPagePreview
            });

            // ── Block name & Attribute Tag ──
            var lblBlock = new Label
            {
                Text = "Block name:",
                Location = new Point(10, 255),
                Size = new Size(80, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            txtBlockName = new TextBox
            {
                Text = "Frame-a4",
                Location = new Point(95, 253),
                Size = new Size(180, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };

            var lblTag = new Label
            {
                Text = "Attribute TAG:",
                Location = new Point(10, 283),
                Size = new Size(85, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            txtAttrTag = new TextBox
            {
                Text = "A00",
                Location = new Point(95, 281),
                Size = new Size(180, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };
            btnChooseTag = new Button
            {
                Text = "Chọn TAG",
                Location = new Point(285, 280),
                Size = new Size(90, 25),
                Font = new Font("Segoe UI", 8.5F)
            };

            // ── Hướng đánh số ──
            var lblDir = new Label
            {
                Text = "Hướng:",
                Location = new Point(10, 313),
                Size = new Size(50, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            cboDirection = new ComboBox
            {
                Location = new Point(65, 311),
                Size = new Size(230, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cboDirection.Items.AddRange(new object[]
            {
                "1. Trái => Phải | Trên => Dưới",
                "2. Trái => Phải | Dưới => Trên",
                "3. Trên => Dưới | Trái => Phải"
            });
            cboDirection.SelectedIndex = 0;

            btnSelectBlocks = new Button
            {
                Text = "Chọn blocks",
                Location = new Point(305, 310),
                Size = new Size(80, 25),
                Font = new Font("Segoe UI", 8.5F)
            };

            tabFrame.Controls.AddRange(new Control[]
            {
                grpRule, grpPage,
                lblBlock, txtBlockName,
                lblTag, txtAttrTag, btnChooseTag,
                lblDir, cboDirection, btnSelectBlocks
            });

            // ═══════════════════════════════════════════
            // TAB 2: STT block attribute (cấu trúc tương tự)
            // ═══════════════════════════════════════════
            tabBlockAttr = new TabPage("STT block attribute") { BackColor = Color.White, Padding = new Padding(10) };

            var grpAttrRule = new GroupBox
            {
                Text = "Quy tắc đánh số thứ tự",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0),
                Location = new Point(10, 5),
                Size = new Size(370, 145)
            };

            var lblAttrPrefixH = new Label { Text = "Prefix", Location = new Point(15, 22), Size = new Size(90, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };
            var lblAttrStyleH = new Label { Text = "Kiểu đánh số", Location = new Point(120, 22), Size = new Size(120, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };
            var lblAttrSuffixH = new Label { Text = "Suffix", Location = new Point(265, 22), Size = new Size(90, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.Black };

            txtAttrPrefix = new TextBox { Location = new Point(15, 42), Size = new Size(90, 23), Font = new Font("Segoe UI", 9F) };
            cboAttrNumberStyle = new ComboBox
            {
                Location = new Point(120, 42), Size = new Size(130, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cboAttrNumberStyle.Items.AddRange(new object[] { "1,2,3,...", "A,B,C,...", "I,II,III,..." });
            cboAttrNumberStyle.SelectedIndex = 0;

            txtAttrSuffix = new TextBox { Location = new Point(265, 42), Size = new Size(90, 23), Font = new Font("Segoe UI", 9F) };

            var lblAttrFrom = new Label { Text = "From:", Location = new Point(15, 75), Size = new Size(40, 20), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black };
            nudAttrFrom = new NumericUpDown { Location = new Point(60, 73), Size = new Size(60, 23), Minimum = 1, Maximum = 9999, Value = 1, Font = new Font("Segoe UI", 9F) };
            chkAttrTwoDigits = new CheckBox { Text = "Sử dụng hai chữ số", Location = new Point(140, 74), Size = new Size(200, 22), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black };

            var lblAttrPreviewLabel = new Label { Text = "Preview:", Location = new Point(15, 105), Size = new Size(60, 20), Font = new Font("Segoe UI", 9F), ForeColor = Color.Black };
            lblAttrPreview = new Label
            {
                Text = "1",
                Location = new Point(80, 103),
                Size = new Size(270, 22),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };

            grpAttrRule.Controls.AddRange(new Control[] {
                lblAttrPrefixH, lblAttrStyleH, lblAttrSuffixH,
                txtAttrPrefix, cboAttrNumberStyle, txtAttrSuffix,
                lblAttrFrom, nudAttrFrom, chkAttrTwoDigits,
                lblAttrPreviewLabel, lblAttrPreview
            });

            var lblAttrBlock = new Label
            {
                Text = "Block name:",
                Location = new Point(10, 165),
                Size = new Size(80, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            txtAttrBlockName = new TextBox
            {
                Text = "",
                Location = new Point(95, 163),
                Size = new Size(180, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };

            var lblAttrTagLabel = new Label
            {
                Text = "Attribute TAG:",
                Location = new Point(10, 195),
                Size = new Size(85, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            txtAttrTagName = new TextBox
            {
                Text = "",
                Location = new Point(95, 193),
                Size = new Size(180, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0)
            };
            btnAttrChooseTag = new Button
            {
                Text = "Chọn TAG",
                Location = new Point(285, 192),
                Size = new Size(90, 25),
                Font = new Font("Segoe UI", 8.5F)
            };

            var lblAttrDir = new Label
            {
                Text = "Hướng:",
                Location = new Point(10, 230),
                Size = new Size(50, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black
            };
            cboAttrDirection = new ComboBox
            {
                Location = new Point(65, 228),
                Size = new Size(230, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cboAttrDirection.Items.AddRange(new object[]
            {
                "1. Trái => Phải | Trên => Dưới",
                "2. Trái => Phải | Dưới => Trên",
                "3. Trên => Dưới | Trái => Phải"
            });
            cboAttrDirection.SelectedIndex = 0;

            btnAttrSelectBlocks = new Button
            {
                Text = "Chọn blocks",
                Location = new Point(305, 227),
                Size = new Size(80, 25),
                Font = new Font("Segoe UI", 8.5F)
            };

            tabBlockAttr.Controls.AddRange(new Control[]
            {
                grpAttrRule,
                lblAttrBlock, txtAttrBlockName,
                lblAttrTagLabel, txtAttrTagName, btnAttrChooseTag,
                lblAttrDir, cboAttrDirection, btnAttrSelectBlocks
            });

            tabControl.TabPages.Add(tabFrame);
            tabControl.TabPages.Add(tabBlockAttr);

            // ─── OK / Cancel ───
            btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(250, 380),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(16, 75, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location = new Point(340, 380),
                Size = new Size(80, 30),
                Font = new Font("Segoe UI", 9F)
            };

            Controls.Add(tabControl);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            // ─── Event Handlers ───
            txtPrefix.TextChanged += (s, e) => UpdatePreview();
            txtSuffix.TextChanged += (s, e) => UpdatePreview();
            nudFrom.ValueChanged += (s, e) => { UpdatePreview(); UpdatePagePreview(); };
            chkTwoDigits.CheckedChanged += (s, e) => { UpdatePreview(); UpdatePagePreview(); };
            cboNumberStyle.SelectedIndexChanged += (s, e) => { UpdatePreview(); UpdatePagePreview(); };

            txtAttrPrefix.TextChanged += (s, e) => UpdateAttrPreview();
            txtAttrSuffix.TextChanged += (s, e) => UpdateAttrPreview();
            nudAttrFrom.ValueChanged += (s, e) => UpdateAttrPreview();
            chkAttrTwoDigits.CheckedChanged += (s, e) => UpdateAttrPreview();
            cboAttrNumberStyle.SelectedIndexChanged += (s, e) => UpdateAttrPreview();

            chkPageMode.CheckedChanged += (s, e) =>
            {
                bool enabled = chkPageMode.Checked;
                chkAutoTotal.Enabled = enabled;
                nudTotalPages.Enabled = enabled && !chkAutoTotal.Checked;
                txtTotalPagesTag.Enabled = enabled;
                txtTotalPagesPrefix.Enabled = enabled;
                UpdatePagePreview();
            };

            chkAutoTotal.CheckedChanged += (s, e) =>
            {
                nudTotalPages.Enabled = chkPageMode.Checked && !chkAutoTotal.Checked;
                UpdatePagePreview();
            };

            nudTotalPages.ValueChanged += (s, e) => UpdatePagePreview();
            txtTotalPagesTag.TextChanged += (s, e) => UpdatePagePreview();
            txtTotalPagesPrefix.TextChanged += (s, e) => UpdatePagePreview();
            txtAttrTag.TextChanged += (s, e) => UpdatePagePreview();

            btnSelectBlocks.Click += (s, e) =>
            {
                UserRequestedSelectBlocks = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            btnAttrSelectBlocks.Click += (s, e) =>
            {
                UserRequestedSelectBlocks = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            btnChooseTag.Click += (s, e) =>
            {
                UserRequestedChooseTag = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            btnAttrChooseTag.Click += (s, e) =>
            {
                UserRequestedChooseTag = true;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        /// <summary>
        /// Cập nhật preview tab khung tên
        /// </summary>
        private void UpdatePreview()
        {
            string num = FormatNumber((int)nudFrom.Value, cboNumberStyle.SelectedIndex, chkTwoDigits.Checked);
            lblPreview.Text = $"{txtPrefix.Text}{num}{txtSuffix.Text}";
        }

        /// <summary>
        /// Cập nhật preview chế độ trang hiện tại/tổng trang
        /// </summary>
        private void UpdatePagePreview()
        {
            if (chkPageMode != null && chkPageMode.Checked)
            {
                string num = FormatNumber((int)nudFrom.Value, cboNumberStyle.SelectedIndex, chkTwoDigits.Checked);
                string totalSample = (chkAutoTotal != null && chkAutoTotal.Checked) ? "3" : FormatNumber((int)nudTotalPages.Value, cboNumberStyle.SelectedIndex, chkTwoDigits.Checked);
                string pageVal = $"{txtPrefix.Text}{num}{txtSuffix.Text}";
                string totalVal = $"{(txtTotalPagesPrefix != null ? txtTotalPagesPrefix.Text : "/")}{totalSample}";
                string tag1 = (txtAttrTag != null && !string.IsNullOrWhiteSpace(txtAttrTag.Text)) ? txtAttrTag.Text : "A00";
                string tag2 = (txtTotalPagesTag != null && !string.IsNullOrWhiteSpace(txtTotalPagesTag.Text)) ? txtTotalPagesTag.Text : "TSHEET";
                lblPagePreview.Text = $"VD: {tag1}='{pageVal}', {tag2}='{totalVal}' (Hiển thị: {pageVal} {totalVal})";
            }
            else if (lblPagePreview != null)
            {
                lblPagePreview.Text = "";
            }
        }

        /// <summary>
        /// Cập nhật preview tab block attribute
        /// </summary>
        private void UpdateAttrPreview()
        {
            string num = FormatNumber((int)nudAttrFrom.Value, cboAttrNumberStyle.SelectedIndex, chkAttrTwoDigits.Checked);
            lblAttrPreview.Text = $"{txtAttrPrefix.Text}{num}{txtAttrSuffix.Text}";
        }

        /// <summary>
        /// Định dạng số thứ tự theo kiểu đánh số
        /// </summary>
        public static string FormatNumber(int number, int styleIndex, bool twoDigits)
        {
            switch (styleIndex)
            {
                case 1: // A,B,C,...
                    return number <= 26 ? ((char)('A' + number - 1)).ToString() : number.ToString();
                case 2: // I,II,III,...
                    return ToRoman(number);
                default: // 1,2,3,...
                    return twoDigits ? number.ToString("D2") : number.ToString();
            }
        }

        /// <summary>
        /// Chuyển đổi số nguyên thành số La Mã
        /// </summary>
        private static string ToRoman(int number)
        {
            if (number <= 0 || number > 3999) return number.ToString();

            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

            return thousands[number / 1000]
                 + hundreds[(number % 1000) / 100]
                 + tens[(number % 100) / 10]
                 + ones[number % 10];
        }
    }
}
