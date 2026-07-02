using System.Drawing.Drawing2D;

namespace NetRouteManager;

public class MainForm : Form
{
    // ── Theme (Teal / Blue palette) ───────────────────────────────────────────
    private static readonly Color BG      = Color.FromArgb(245, 252, 249);  // near-white mint
    private static readonly Color BG2     = Color.FromArgb(255, 255, 255);  // white
    private static readonly Color BG3     = Color.FromArgb(225, 245, 238);  // light teal
    private static readonly Color FG      = Color.FromArgb(18,  40,  55);   // dark navy
    private static readonly Color FG2     = Color.FromArgb(80,  120, 135);  // medium teal-slate
    private static readonly Color ACCENT  = Color.FromArgb(72,  135, 183);  // #4887B7
    private static readonly Color GREEN   = Color.FromArgb(54,  112, 150);  // #367096 (Launch & success)
    private static readonly Color RED     = Color.FromArgb(210, 55,  55);   // danger
    private static readonly Color BORDER  = Color.FromArgb(143, 219, 197);  // #8FDBC5 teal
    private static readonly Color SEL_BG  = Color.FromArgb(219, 247, 210);  // #DBF7D2 mint
    private static readonly Color SEL_FG  = Color.FromArgb(40,  100, 130);  // dark teal

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly LauncherManager _lm = new();

    // ── Controls ──────────────────────────────────────────────────────────────
    private DataGridView  _gridApps    = null!;
    private DataGridView  _gridBinding = null!;
    private RichTextBox   _log         = null!;
    private Button        _btnLaunch   = null!;
    private Button        _btnEdit     = null!;
    private Button        _btnDelete   = null!;
    private Button        _btnRelease  = null!;
    private SplitContainer _splitMain  = null!;   // Configured Apps vs bottom
    private SplitContainer _splitBot   = null!;   // Active Bindings vs Log
    private System.Windows.Forms.Timer _bindingTimer = null!;

    public MainForm()
    {
        Text          = "NetRoute Manager";
        Size          = new Size(860, 680);
        MinimumSize   = new Size(720, 560);
        BackColor     = BG;
        ForeColor     = FG;
        Font          = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;

        using var stream = typeof(MainForm).Assembly
            .GetManifestResourceStream("NetRouteManager.app.ico");
        if (stream is not null) Icon = new Icon(stream);

        BuildUI();
        RefreshAppList();

        Shown += (_, _) =>
        {
            _gridApps.ClearSelection();
            _gridApps.CurrentCell = null;
            // Set initial splitter positions as proportions of actual client height
            int available = ClientSize.Height - 48; // subtract header
            _splitMain.SplitterDistance = Math.Max(120, (int)(available * 0.55));
            _splitBot.SplitterDistance  = Math.Max(80,  (int)(_splitBot.Height * 0.60));
        };

        _bindingTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _bindingTimer.Tick += (_, _) => RefreshBindings();
        _bindingTimer.Start();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Inner split: Active Bindings ↕ Log ───────────────────────────────
        _splitBot = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Horizontal,
            BackColor     = BORDER,        // splitter bar color
            SplitterWidth = 5,
            Panel1MinSize = 80,
            Panel2MinSize = 50,
            FixedPanel    = FixedPanel.None,
        };
        _splitBot.Panel1.BackColor = BG;
        _splitBot.Panel2.BackColor = BG;
        _splitBot.Panel1.Controls.Add(BuildBindings());
        _splitBot.Panel2.Controls.Add(BuildLog());

        // ── Outer split: (AppGrid + ActionBar) ↕ bottom section ─────────────
        _splitMain = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Horizontal,
            BackColor     = BG3,
            SplitterWidth = 5,
            Panel1MinSize = 120,
            Panel2MinSize = 130,
            FixedPanel    = FixedPanel.None,
        };
        _splitMain.Panel1.BackColor = BG;
        _splitMain.Panel2.BackColor = BG;

        // Top panel: app grid (fills) + action bar (44px fixed at bottom)
        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = BG,
            RowCount = 2, ColumnCount = 1,
        };
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        topLayout.Controls.Add(BuildAppGrid(),   0, 0);
        topLayout.Controls.Add(BuildActionBar(), 0, 1);
        _splitMain.Panel1.Controls.Add(topLayout);
        _splitMain.Panel2.Controls.Add(_splitBot);

        // DockStyle.Fill harus ditambahkan SEBELUM DockStyle.Top
        // WinForms memproses Controls dari index tertinggi lebih dulu;
        // header (Top) harus di index lebih tinggi agar diklaim duluan.
        Controls.Add(_splitMain);   // index 0 → Fill, diproses kedua

        var header = BuildHeader();
        header.Dock   = DockStyle.Top;
        header.Height = 48;
        Controls.Add(header);       // index 1 → Top, diproses pertama → klaim 48px atas
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private Panel BuildHeader()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BG2 };

        // Left accent bar
        p.Controls.Add(new Panel
        {
            Dock = DockStyle.Left, Width = 4, BackColor = ACCENT,
        });
        // Bottom separator
        p.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom, Height = 1, BackColor = BORDER,
        });

        var title = new Label
        {
            Text      = "NetRoute Manager",
            Font      = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = FG,
            AutoSize  = true,
            Top       = 12, Left = 20,
        };

        // "Run as Administrator" badge
        var badge = new Label
        {
            Text      = "  ▲  Run as Administrator  ",
            Font      = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = ACCENT,
            BackColor = SEL_BG,
            AutoSize  = true,
            Top       = 15,
            Padding   = new Padding(0, 3, 0, 3),
        };
        void PositionBadge() => badge.Left = p.Width - badge.Width - 16;
        p.Resize    += (_, _) => PositionBadge();
        p.VisibleChanged += (_, _) => PositionBadge();

        p.Controls.Add(title);
        p.Controls.Add(badge);
        return p;
    }

    // ── App list grid ─────────────────────────────────────────────────────────

    private Panel BuildAppGrid()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BG, Padding = new Padding(12, 8, 12, 0) };

        var hdr = new Label
        {
            Text      = "CONFIGURED APPS",
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = FG2,
            AutoSize  = true,
            Top = 2, Left = 0,
        };
        p.Controls.Add(hdr);

        _gridApps = MakeGrid(new[] { "App Name", "Adapter", "Executable", "Arguments" },
                             new[] { 160, 180, 310, 120 });

        // Checkbox column inserted at index 0
        _gridApps.Columns.Insert(0, new DataGridViewCheckBoxColumn
        {
            HeaderText = "", Width = 30, Resizable = DataGridViewTriState.False,
            SortMode   = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = BG2,
                SelectionBackColor = BG2,
                Alignment          = DataGridViewContentAlignment.MiddleCenter,
            },
            HeaderCell = { Style = new DataGridViewCellStyle
            {
                BackColor          = BG3,
                SelectionBackColor = BG3,
            }},
        });

        // Toggle checkbox on click (grid is ReadOnly so we toggle programmatically)
        _gridApps.CellClick += (_, e) =>
        {
            if (e.ColumnIndex != 0 || e.RowIndex < 0) return;
            bool cur = _gridApps.Rows[e.RowIndex].Cells[0].Value as bool? ?? false;
            _gridApps.Rows[e.RowIndex].Cells[0].Value = !cur;
            UpdateButtonStates();
        };

        _gridApps.Top    = 26;
        _gridApps.Left   = 0;
        _gridApps.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                           AnchorStyles.Right | AnchorStyles.Bottom;
        _gridApps.Width  = p.ClientSize.Width - 0;
        _gridApps.Height = p.ClientSize.Height - 26;
        _gridApps.SelectionChanged += (_, _) => UpdateButtonStates();
        _gridApps.DoubleClick      += (_, _) => LaunchSelected();
        _gridApps.MouseClick       += (_, e) =>
        {
            if (_gridApps.HitTest(e.X, e.Y).RowIndex < 0)
                _gridApps.ClearSelection();
        };
        p.Resize += (_, _) =>
        {
            _gridApps.Width  = p.ClientSize.Width;
            _gridApps.Height = p.ClientSize.Height - 26;
        };
        p.Controls.Add(_gridApps);
        return p;
    }

    // ── Action bar ────────────────────────────────────────────────────────────

    private Panel BuildActionBar()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BG2, Padding = new Padding(12, 5, 12, 5) };
        // Top separator
        p.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BORDER });

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = BG2,
            Padding = new Padding(0, 3, 0, 0),
        };

        _btnLaunch = MakeButton("▶  Launch", GREEN,  Color.White, 110);
        var btnAdd  = MakeButton("+ Add",     ACCENT, Color.White, 80);
        _btnEdit   = MakeButton("Edit",   BG3,   FG,         74, outline: true);
        _btnDelete = MakeButton("Delete", RED,   Color.White, 80);

        _btnLaunch.Click  += (_, _) => LaunchSelected();
        btnAdd.Click      += (_, _) => OpenAddDialog();
        _btnEdit.Click    += (_, _) => OpenEditDialog();
        _btnDelete.Click  += (_, _) => DeleteChecked();
        _btnLaunch.Enabled = false;
        _btnEdit.Enabled   = false;
        _btnDelete.Enabled = false;

        flow.Controls.AddRange(new Control[] { _btnLaunch, btnAdd, _btnEdit, _btnDelete });
        p.Controls.Add(flow);
        return p;
    }

    // ── Active bindings ───────────────────────────────────────────────────────

    private Panel BuildBindings()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BG, Padding = new Padding(12, 4, 12, 4) };

        var hdr = new Label
        {
            Text      = "ACTIVE BINDINGS  —  adapter locked while app is running",
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = FG2,
            AutoSize  = true,
            Top = 2, Left = 0,
        };
        p.Controls.Add(hdr);

        _gridBinding = MakeGrid(new[] { "App Name", "PID", "Adapter", "Running Since" },
                                new[] { 170, 70, 220, 120 });
        _gridBinding.Top    = 22;
        _gridBinding.Left   = 0;
        _gridBinding.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _gridBinding.Width  = p.ClientSize.Width;
        _gridBinding.Height = p.ClientSize.Height - 58;
        _gridBinding.DefaultCellStyle.ForeColor = GREEN;
        _gridBinding.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
        _gridBinding.SelectionChanged += (_, _) => UpdateButtonStates();
        p.Resize += (_, _) =>
        {
            _gridBinding.Width  = p.ClientSize.Width;
            _gridBinding.Height = p.ClientSize.Height - 58;
        };
        p.Controls.Add(_gridBinding);

        _btnRelease = MakeButton("Release Adapter", RED, Color.White, 160);
        _btnRelease.Enabled = false;
        _btnRelease.Top     = p.ClientSize.Height - 34;
        _btnRelease.Left    = 0;
        _btnRelease.Anchor  = AnchorStyles.Bottom | AnchorStyles.Left;
        _btnRelease.Click  += (_, _) => ReleaseSelected();
        p.Resize += (_, _) => _btnRelease.Top = p.ClientSize.Height - 34;
        p.Controls.Add(_btnRelease);
        return p;
    }

    // ── Log ───────────────────────────────────────────────────────────────────

    private Panel BuildLog()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BG, Padding = new Padding(12, 0, 12, 8) };
        var hdr = new Label
        {
            Text = "LOG", Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = FG2, AutoSize = true, Top = 2, Left = 0,
        };
        _log = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = BG3,
            ForeColor   = FG,
            Font        = new Font("Consolas", 9),
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            Top = 18,
        };
        _log.Top    = 20;
        _log.Left   = 0;
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                      AnchorStyles.Right | AnchorStyles.Bottom;
        _log.Width  = p.ClientSize.Width;
        _log.Height = p.ClientSize.Height - 20;
        p.Resize += (_, _) =>
        {
            _log.Width  = p.ClientSize.Width;
            _log.Height = p.ClientSize.Height - 20;
        };
        AppendLog("Ready. Double-click an app to launch it.", FG2);
        p.Controls.Add(hdr);
        p.Controls.Add(_log);
        return p;
    }

    // ── App list operations ───────────────────────────────────────────────────

    private void RefreshAppList()
    {
        _gridApps.Rows.Clear();
        foreach (var l in _lm.GetAll())
            _gridApps.Rows.Add(false, l.Name, l.AdapterName, l.ExePath,
                               string.IsNullOrEmpty(l.Arguments) ? "—" : l.Arguments);
        _gridApps.ClearSelection();
        _gridApps.CurrentCell = null;
        UpdateButtonStates();
    }

    private List<AppLauncher> GetCheckedLaunchers()
    {
        var all    = _lm.GetAll();
        var result = new List<AppLauncher>();
        for (int i = 0; i < _gridApps.Rows.Count && i < all.Count; i++)
            if (_gridApps.Rows[i].Cells[0].Value as bool? == true)
                result.Add(all[i]);
        return result;
    }

    private AppLauncher? SelectedLauncher()
    {
        if (_gridApps.SelectedRows.Count == 0) return null;
        int idx = _gridApps.SelectedRows[0].Index;
        var all  = _lm.GetAll();
        return idx >= 0 && idx < all.Count ? all[idx] : null;
    }

    private void UpdateButtonStates()
    {
        int n = GetCheckedLaunchers().Count;
        _btnLaunch.Enabled  = _gridApps.SelectedRows.Count > 0;
        _btnEdit.Enabled    = n > 0;
        _btnDelete.Enabled  = n > 0;
        _btnEdit.Text       = n > 1 ? $"Edit ({n})"   : "Edit";
        _btnDelete.Text     = n > 1 ? $"Delete ({n})" : "Delete";
        _btnRelease.Enabled = _gridBinding.SelectedRows.Count > 0;
    }

    private void OpenAddDialog()
    {
        using var dlg = new LauncherDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _lm.Add(dlg.Result);
            RefreshAppList();
        }
    }

    private void OpenEditDialog()
    {
        var checked_ = GetCheckedLaunchers();
        if (checked_.Count == 0) return;

        if (checked_.Count == 1)
        {
            // Single edit: full dialog
            var target = checked_[0];
            using var dlg = new LauncherDialog(target);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lm.Update(target, dlg.Result);
                RefreshAppList();
            }
        }
        else
        {
            // Bulk edit: change adapter only
            OpenBulkAdapterDialog(checked_);
        }
    }

    private void OpenBulkAdapterDialog(List<AppLauncher> targets)
    {
        var adapters = NetworkUtils.GetAdapters()
            .Where(a => a.Status == "Connected")
            .Select(a => a.Name)
            .ToArray();

        using var dlg = new Form
        {
            Text = $"Edit {targets.Count} Apps — Change Adapter",
            Size = new Size(420, 170),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = BG, ForeColor = FG,
            Font = new Font("Segoe UI", 10),
        };

        var lbl = new Label
        {
            Text = $"Set adapter for {targets.Count} selected apps:",
            ForeColor = FG2, Font = new Font("Segoe UI", 9),
            Left = 16, Top = 16, AutoSize = true,
        };
        var cmb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 16, Top = 38, Width = 372,
            BackColor = BG2, ForeColor = FG, FlatStyle = FlatStyle.Flat,
        };
        cmb.Items.AddRange(adapters);
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;

        var btnApply  = new Button
        {
            Text = "Apply", Left = 220, Top = 92, Width = 80, Height = 30,
            BackColor = ACCENT, ForeColor = BG, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK,
        };
        var btnCancel = new Button
        {
            Text = "Cancel", Left = 308, Top = 92, Width = 80, Height = 30,
            BackColor = BG3, ForeColor = FG, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel,
        };

        dlg.Controls.AddRange([lbl, cmb, btnApply, btnCancel]);
        dlg.AcceptButton = btnApply;
        dlg.CancelButton = btnCancel;

        if (dlg.ShowDialog(this) == DialogResult.OK && cmb.SelectedItem is string adapter)
        {
            foreach (var t in targets)
                _lm.Update(t, new AppLauncher
                {
                    Name        = t.Name,
                    ExePath     = t.ExePath,
                    Arguments   = t.Arguments,
                    AdapterName = adapter,
                });
            RefreshAppList();
            AppendLog($"Adapter updated to '{adapter}' for {targets.Count} apps.", GREEN);
        }
    }

    private void DeleteChecked()
    {
        var targets = GetCheckedLaunchers();
        if (targets.Count == 0) return;

        string msg = targets.Count == 1
            ? $"Delete '{targets[0].Name}'?"
            : $"Delete {targets.Count} selected apps?";

        if (MessageBox.Show(msg, "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            foreach (var t in targets) _lm.Remove(t);
            RefreshAppList();
        }
    }

    private void LaunchSelected()
    {
        var sel = SelectedLauncher();
        if (sel is null) return;
        AppendLog($"Launching '{sel.Name}' via {sel.AdapterName}…", ACCENT);
        _ = _lm.LaunchAsync(sel, msg => BeginInvoke(() => AppendLog(msg, GREEN)));
    }

    // ── Active bindings ───────────────────────────────────────────────────────

    private void RefreshBindings()
    {
        var bindings = _lm.GetActiveBindings().ToList();
        var pids     = bindings.Select(b => b.Pid).ToHashSet();

        // Remove rows no longer active
        for (int i = _gridBinding.Rows.Count - 1; i >= 0; i--)
        {
            if (_gridBinding.Rows[i].Cells[1].Value is int pid && !pids.Contains(pid))
                _gridBinding.Rows.RemoveAt(i);
        }

        // Add or update rows
        foreach (var b in bindings)
        {
            var existing = _gridBinding.Rows.Cast<DataGridViewRow>()
                               .FirstOrDefault(r => r.Cells[1].Value is int p && p == b.Pid);
            string[] vals = [b.Launcher.Name, b.Pid.ToString(),
                             b.Launcher.AdapterName, b.StartedAt.ToString("HH:mm:ss")];
            if (existing is not null)
                for (int i = 0; i < vals.Length; i++) existing.Cells[i].Value = vals[i];
            else
                _gridBinding.Rows.Add(vals);
        }

        UpdateButtonStates();
    }

    private void ReleaseSelected()
    {
        if (_gridBinding.SelectedRows.Count == 0) return;
        if (_gridBinding.SelectedRows[0].Cells[1].Value is not string pidStr) return;
        if (!int.TryParse(pidStr, out int pid)) return;

        if (MessageBox.Show(
                $"Restore adapter metrics now for PID {pid}?\n\nThe app stays running but may switch to the default adapter.",
                "Release Adapter", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _lm.Release(pid, msg => BeginInvoke(() => AppendLog(msg, GREEN)));
        }
    }

    // ── Log ───────────────────────────────────────────────────────────────────

    private void AppendLog(string msg, Color color)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        _log.SelectionStart  = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor  = color;
        _log.AppendText($"[{ts}] {msg}{Environment.NewLine}");
        _log.ScrollToCaret();
    }

    // ── Widget factories ──────────────────────────────────────────────────────

    private DataGridView MakeGrid(string[] headers, int[] widths)
    {
        var dgv = new DataGridView
        {
            BackgroundColor           = BG2,
            GridColor                 = BORDER,
            BorderStyle               = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible         = false,
            SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect               = false,
            ReadOnly                  = true,
            AllowUserToAddRows        = false,
            AllowUserToDeleteRows     = false,
            AllowUserToResizeRows     = false,
            ColumnHeadersHeight       = 30,
            RowTemplate               = { Height = 28 },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = BG2,
                ForeColor          = FG,
                SelectionBackColor = SEL_BG,
                SelectionForeColor = SEL_FG,
                Font               = new Font("Segoe UI", 9),
                Padding            = new Padding(4, 0, 4, 0),
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = BG3,
                ForeColor          = FG2,
                SelectionBackColor = BG3,
                SelectionForeColor = FG2,
                Font               = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Alignment          = DataGridViewContentAlignment.MiddleLeft,
                Padding            = new Padding(4, 0, 4, 0),
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 251, 252),
            },
        };

        for (int i = 0; i < headers.Length; i++)
        {
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headers[i],
                Width      = widths[i],
                SortMode   = DataGridViewColumnSortMode.NotSortable,
            });
        }
        dgv.Columns[^1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        return dgv;
    }

    private static Button MakeButton(string text, Color back, Color fore,
                                     int width = 90, bool outline = false)
    {
        var btn = new Button
        {
            Text      = text,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Size      = new Size(width, 34),
            Margin    = new Padding(0, 0, 6, 0),
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderSize           = 0;
        btn.FlatAppearance.BorderColor          = back;
        btn.FlatAppearance.MouseOverBackColor   = back;
        btn.FlatAppearance.MouseDownBackColor   = back;

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

            // Clear corners using parent background
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
        path.AddArc(rect.X,              rect.Y,               d, d, 180, 90);
        path.AddArc(rect.Right - d,      rect.Y,               d, d, 270, 90);
        path.AddArc(rect.Right - d,      rect.Bottom - d,      d, d,   0, 90);
        path.AddArc(rect.X,              rect.Bottom - d,      d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Shift(Color c, int delta) => Color.FromArgb(
        c.A,
        Math.Clamp(c.R + delta, 0, 255),
        Math.Clamp(c.G + delta, 0, 255),
        Math.Clamp(c.B + delta, 0, 255));
}
