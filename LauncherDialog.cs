using System.Drawing.Drawing2D;

namespace NetRouteManager;

public class LauncherDialog : Form
{
    private static readonly Color BG     = Color.FromArgb(245, 252, 249);
    private static readonly Color BG2    = Color.FromArgb(255, 255, 255);
    private static readonly Color BG3    = Color.FromArgb(225, 245, 238);
    private static readonly Color FG     = Color.FromArgb(18,  40,  55);
    private static readonly Color FG2    = Color.FromArgb(80,  120, 135);
    private static readonly Color ACCENT = Color.FromArgb(72,  135, 183);
    private static readonly Color GREEN  = Color.FromArgb(54,  112, 150);
    private static readonly Color RED    = Color.FromArgb(210, 55,  55);
    private static readonly Color BORDER = Color.FromArgb(143, 219, 197);
    private static readonly Color SEL_BG = Color.FromArgb(219, 247, 210);
    private static readonly Color SEL_FG = Color.FromArgb(40,  100, 130);

    private const int COL_ICON = 0;
    private const int COL_NAME = 1;
    private const int COL_EXE  = 2;

    private static readonly Bitmap _blankIcon =
        new(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

    private readonly TextBox      _txtName;
    private readonly TextBox      _txtExe;
    private readonly TextBox      _txtArgs;
    private readonly ComboBox     _cmbAdapter;

    // Only used in Add mode
    private TextBox?      _txtSearch;
    private DataGridView? _grid;
    private Label?        _lblStatus;
    private List<InstalledApp> _allApps = [];
    private int           _iconSeq  = 0;

    public AppLauncher Result { get; private set; } = new();

    public LauncherDialog(AppLauncher? existing = null)
    {
        bool isEdit = existing is not null;

        Text            = isEdit ? "Edit App" : "Add App";
        FormBorderStyle = isEdit ? FormBorderStyle.FixedDialog : FormBorderStyle.Sizable;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = BG;
        ForeColor       = FG;
        Font            = new Font("Segoe UI", 10);

        _txtName    = MakeEntry(existing?.Name      ?? "");
        _txtExe     = MakeEntry(existing?.ExePath   ?? "");
        _txtArgs    = MakeEntry(existing?.Arguments  ?? "");
        _cmbAdapter = BuildAdapterCombo(existing?.AdapterName);

        if (isEdit)
            BuildEditLayout();
        else
            BuildAddLayout();
    }

    // ── Edit layout: simple form, no app picker ───────────────────────────────

    private void BuildEditLayout()
    {
        Size = new Size(560, 310);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(16),
            ColumnCount = 3, RowCount = 5, BackColor = BG,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (int i = 0; i < 5; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(table);

        var btnBrowse = MakeButton("Browse…", BG3, FG, outline: true);
        btnBrowse.Click += OnBrowse;

        // Exe field + Browse
        _txtExe.Font     = new Font("Segoe UI", 10);
        _txtExe.ReadOnly = false;

        table.Controls.Add(MakeLabel("App Name:"),   0, 0);
        table.Controls.Add(_txtName,                 1, 0);
        table.SetColumnSpan(_txtName, 2);

        table.Controls.Add(MakeLabel("Executable:"), 0, 1);
        table.Controls.Add(_txtExe,                  1, 1);
        table.Controls.Add(btnBrowse,                2, 1);

        table.Controls.Add(MakeLabel("Arguments:"),  0, 2);
        table.Controls.Add(_txtArgs,                 1, 2);
        table.SetColumnSpan(_txtArgs, 2);

        table.Controls.Add(MakeLabel("Use Adapter:"),0, 3);
        table.Controls.Add(_cmbAdapter,              1, 3);
        table.SetColumnSpan(_cmbAdapter, 2);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            BackColor = BG,
        };
        var btnSave   = MakeButton("Save",   ACCENT, Color.White);
        var btnCancel = MakeButton("Cancel", BG3,    FG, outline: true);
        btnSave.Click   += OnSave;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnPanel.Controls.AddRange([btnSave, btnCancel]);
        table.Controls.Add(btnPanel, 0, 4);
        table.SetColumnSpan(btnPanel, 3);
    }

    // ── Add layout: app name + installed apps picker + fields ─────────────────

    private void BuildAddLayout()
    {
        Size       = new Size(720, 580);
        MinimumSize = new Size(600, 480);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(14),
            ColumnCount = 1, RowCount = 3, BackColor = BG,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        Controls.Add(root);

        // Row 0: App Name
        var nameRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BG,
        };
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nameRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nameRow.Controls.Add(MakeLabel("App Name:"), 0, 0);
        nameRow.Controls.Add(_txtName,               1, 0);
        root.Controls.Add(nameRow, 0, 0);

        // Row 1: Installed apps picker
        var pickerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = BG,
            Margin = new Padding(0, 4, 0, 4),
        };
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pickerPanel.Controls.Add(MakeLabel("Installed Apps:"), 0, 0);

        // Search + Browse
        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BG,
            Margin = new Padding(0, 2, 0, 2),
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍  Search by name or path…",
            BackColor = BG2, ForeColor = FG,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
        };
        _txtSearch.TextChanged += (_, _) => ApplyFilter(_txtSearch.Text);

        var btnBrowse = MakeButton("Browse…", BG3, FG, outline: true);
        btnBrowse.Dock   = DockStyle.Fill;
        btnBrowse.Margin = new Padding(6, 0, 0, 0);
        btnBrowse.Click += OnBrowse;

        searchRow.Controls.Add(_txtSearch, 0, 0);
        searchRow.Controls.Add(btnBrowse,  1, 0);
        pickerPanel.Controls.Add(searchRow, 0, 1);

        // Grid
        _grid = new DataGridView
        {
            Dock                       = DockStyle.Fill,
            BackgroundColor            = BG2,
            GridColor                  = BORDER,
            BorderStyle                = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles  = false,
            RowHeadersVisible          = false,
            SelectionMode              = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                = false,
            ReadOnly                   = true,
            AllowUserToAddRows         = false,
            AllowUserToDeleteRows      = false,
            AllowUserToResizeRows      = false,
            ColumnHeadersHeight        = 26,
            RowTemplate                = { Height = 22 },
            Cursor                     = Cursors.Hand,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = BG2, ForeColor = FG,
                SelectionBackColor = SEL_BG, SelectionForeColor = SEL_FG,
                Font               = new Font("Segoe UI", 9),
                Padding            = new Padding(4, 0, 4, 0),
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = BG3, ForeColor = FG2,
                SelectionBackColor = BG3, SelectionForeColor = FG2,
                Font               = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Padding            = new Padding(4, 0, 4, 0),
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 251, 252),
            },
        };

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "", Width = 24, Resizable = DataGridViewTriState.False,
            SortMode   = DataGridViewColumnSortMode.NotSortable,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BG2, SelectionBackColor = SEL_BG, Padding = new Padding(3),
            },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Name", Width = 195, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Executable", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
              SortMode = DataGridViewColumnSortMode.NotSortable });

        _grid.SelectionChanged += OnGridSelectionChanged;
        _grid.CellDoubleClick  += (_, _) => { _txtName.Focus(); _txtName.SelectAll(); };
        pickerPanel.Controls.Add(_grid, 0, 2);
        root.Controls.Add(pickerPanel, 0, 1);

        // Row 2: Exe path + Args + Adapter + Buttons
        var bottomTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = BG,
            Margin = new Padding(0, 4, 0, 0),
        };
        bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            bottomTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // Exe, Args, Adapter
        bottomTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));      // Buttons row

        _txtExe.Font        = new Font("Segoe UI", 9);
        _txtExe.BackColor   = BG3;
        _txtExe.ForeColor   = FG2;
        _txtExe.PlaceholderText = "Select from list above or use Browse…";

        _lblStatus = new Label
        {
            Text = "Loading…", ForeColor = FG2,
            Font = new Font("Segoe UI", 8),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0),
        };

        var exeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BG, Margin = new Padding(0),
        };
        exeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        exeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        exeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        exeRow.Controls.Add(_txtExe,    0, 0);
        exeRow.Controls.Add(_lblStatus, 1, 0);

        bottomTable.Controls.Add(MakeLabel("Executable:"),  0, 0);
        bottomTable.Controls.Add(exeRow,                    1, 0);
        bottomTable.Controls.Add(MakeLabel("Arguments:"),   0, 1);
        bottomTable.Controls.Add(_txtArgs,                  1, 1);
        bottomTable.Controls.Add(MakeLabel("Use Adapter:"), 0, 2);
        bottomTable.Controls.Add(_cmbAdapter,               1, 2);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            BackColor = BG, Margin = new Padding(0),
        };
        var btnSave   = MakeButton("Save",   ACCENT, Color.White);
        var btnCancel = MakeButton("Cancel", BG3,    FG, outline: true);
        btnSave.Click   += OnSave;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnPanel.Controls.AddRange([btnSave, btnCancel]);
        bottomTable.Controls.Add(btnPanel, 1, 3);
        root.Controls.Add(bottomTable, 0, 2);

        Shown += async (_, _) => await LoadAppsAsync();
    }

    // ── App loading (Add mode only) ───────────────────────────────────────────

    private async Task LoadAppsAsync()
    {
        if (_lblStatus is null) return;
        _lblStatus.ForeColor = FG2;
        _lblStatus.Text      = "Scanning…";
        try
        {
            _allApps = await Task.Run(InstalledAppsProvider.GetAll);
            ApplyFilter("");
            _lblStatus.ForeColor = GREEN;
            _lblStatus.Text      = $"{_allApps.Count} apps found";
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = RED;
            _lblStatus.Text      = $"Error: {ex.Message}";
        }
    }

    private void ApplyFilter(string query)
    {
        if (_grid is null) return;
        _grid.Rows.Clear();
        var list = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.ExePath.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var exePaths = new List<string>(list.Count);
        foreach (var app in list)
        {
            _grid.Rows.Add(_blankIcon, app.Name, app.ExePath);
            exePaths.Add(app.ExePath);
        }

        if (!string.IsNullOrWhiteSpace(query) && _lblStatus is not null)
            _lblStatus.Text = $"{list.Count} / {_allApps.Count} apps";

        _ = LoadIconsAsync(exePaths);
    }

    private async Task LoadIconsAsync(List<string> exePaths)
    {
        if (_grid is null) return;
        int seq = ++_iconSeq;

        var bitmaps = await Task.Run(() =>
            exePaths.Select(p =>
            {
                try { using var ico = Icon.ExtractAssociatedIcon(p);
                      return ico is null ? null : new Bitmap(ico.ToBitmap(), 16, 16); }
                catch { return (Bitmap?)null; }
            }).ToList()
        );

        if (seq != _iconSeq || _grid is null) return;
        for (int i = 0; i < Math.Min(bitmaps.Count, _grid.Rows.Count); i++)
            if (bitmaps[i] is not null)
                _grid.Rows[i].Cells[COL_ICON].Value = bitmaps[i];
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid is null || _grid.SelectedRows.Count == 0) return;
        _txtExe.Text  = _grid.SelectedRows[0].Cells[COL_EXE ].Value?.ToString() ?? "";
        _txtName.Text = _grid.SelectedRows[0].Cells[COL_NAME].Value?.ToString() ?? "";
    }

    // ── Shared events ─────────────────────────────────────────────────────────

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Executable",
            Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _txtExe.Text = dlg.FileName;
        if (string.IsNullOrWhiteSpace(_txtName.Text))
            _txtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text) ||
            string.IsNullOrWhiteSpace(_txtExe.Text)  ||
            _cmbAdapter.SelectedItem is null)
        {
            MessageBox.Show("App Name, Executable, and Adapter are required.",
                            "Missing Fields", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Result = new AppLauncher
        {
            Name        = _txtName.Text.Trim(),
            ExePath     = _txtExe.Text.Trim(),
            AdapterName = _cmbAdapter.SelectedItem.ToString()!,
            Arguments   = _txtArgs.Text.Trim(),
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ComboBox BuildAdapterCombo(string? selected)
    {
        var cmb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            BackColor = BG2, ForeColor = FG,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0),
        };
        var adapters = NetworkUtils.GetAdapters()
            .Where(a => a.Status == "Connected")
            .Select(a => a.Name)
            .ToArray();
        cmb.Items.AddRange(adapters);
        if (selected is not null && adapters.Contains(selected))
            cmb.SelectedItem = selected;
        else if (adapters.Length > 0)
            cmb.SelectedIndex = 0;
        return cmb;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = FG2, Font = new Font("Segoe UI", 9),
        Margin = new Padding(0, 0, 6, 0),
    };

    private static TextBox MakeEntry(string value) => new()
    {
        Text = value, BackColor = BG2, ForeColor = FG,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10),
        Margin = new Padding(0),
    };

    private static Button MakeButton(string text, Color back, Color fore,
                                     bool outline = false)
    {
        var btn = new Button
        {
            Text = text, BackColor = back, ForeColor = fore,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(80, 32), Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderSize          = 0;
        btn.FlatAppearance.BorderColor         = back;
        btn.FlatAppearance.MouseOverBackColor  = back;
        btn.FlatAppearance.MouseDownBackColor  = back;

        bool hover = false, pressed = false;
        btn.MouseEnter += (_, _) => { hover   = true;  btn.Invalidate(); };
        btn.MouseLeave += (_, _) => { hover   = false; pressed = false; btn.Invalidate(); };
        btn.MouseDown  += (_, _) => { pressed = true;  btn.Invalidate(); };
        btn.MouseUp    += (_, _) => { pressed = false; btn.Invalidate(); };

        btn.Paint += (s, pe) =>
        {
            var g = pe.Graphics;
            g.SmoothingMode   = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            Color parentBg = btn.Parent?.BackColor ?? BG2;
            using (var br = new SolidBrush(parentBg))
                g.FillRectangle(br, 0, 0, btn.Width, btn.Height);

            Color fill = outline
                ? (pressed ? Shift(BG3, -20) : hover ? Shift(BG3, -10) : BG3)
                : (pressed ? Shift(back, -25) : hover ? Shift(back, -15) : back);

            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            using var path = RoundedPath(rect, 6);
            using (var br = new SolidBrush(fill))
                g.FillPath(br, path);

            if (outline)
            {
                using var pen = new Pen(BORDER, 1.5f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, btn.Text, btn.Font,
                new Rectangle(0, 0, btn.Width, btn.Height),
                btn.Enabled ? fore : FG2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        return btn;
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X,         rect.Y,          d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y,          d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d,   0, 90);
        path.AddArc(rect.X,         rect.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Shift(Color c, int delta) => Color.FromArgb(
        c.A,
        Math.Clamp(c.R + delta, 0, 255),
        Math.Clamp(c.G + delta, 0, 255),
        Math.Clamp(c.B + delta, 0, 255));
}
