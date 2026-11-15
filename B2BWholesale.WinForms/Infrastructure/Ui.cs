using System.Globalization;

namespace B2BWholesale.WinForms.Infrastructure;

/// <summary>Shared look and feel plus small layout helpers so every screen looks consistent.</summary>
public static class Ui
{
    public static readonly Color Primary = Color.FromArgb(31, 78, 121);
    public static readonly Color PrimaryLight = Color.FromArgb(232, 240, 248);
    public static readonly Color Success = Color.FromArgb(25, 135, 84);
    public static readonly Color Danger = Color.FromArgb(200, 35, 51);
    public static readonly Color Warning = Color.FromArgb(255, 193, 7);
    public static readonly Color Muted = Color.FromArgb(108, 117, 125);
    public static readonly Color Surface = Color.FromArgb(247, 249, 252);

    public static readonly Font BaseFont = new("Segoe UI", 10F);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 16F);
    public static readonly Font BoldFont = new("Segoe UI Semibold", 10F);

    public static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    public static string Money(decimal v) => v.ToString("N0", Vn) + " ₫";

    public static Label Title(string text) => new()
    {
        Text = text, Font = TitleFont, ForeColor = Primary, AutoSize = true,
        Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8)
    };

    public static Button Btn(string text, Color? back = null, int width = 110)
    {
        var b = new Button
        {
            Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat,
            BackColor = back ?? Primary, ForeColor = back == Warning ? Color.Black : Color.White,
            Font = BoldFont, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle, RowHeadersVisible = false, EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 34, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            Font = BaseFont
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Primary;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = BoldFont;
        g.AlternatingRowsDefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 226, 243);
        g.DefaultCellStyle.SelectionForeColor = Color.Black;
        g.RowTemplate.Height = 28;
        return g;
    }

    /// <summary>Formats/renames columns after binding. Unknown names are ignored.</summary>
    public static void Columns(DataGridView g, params (string name, string header, string? format, float weight)[] cols)
    {
        foreach (DataGridViewColumn c in g.Columns) c.Visible = false;
        var order = 0;
        foreach (var (name, header, format, weight) in cols)
        {
            if (!g.Columns.Contains(name)) continue;
            var c = g.Columns[name]!;
            c.Visible = true;
            c.HeaderText = header;
            c.DisplayIndex = order++;
            c.FillWeight = weight;
            if (format != null)
            {
                c.DefaultCellStyle.Format = format;
                c.DefaultCellStyle.FormatProvider = Vn;
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
    }

    /// <summary>A two-column label/control form layout.</summary>
    public static TableLayoutPanel FieldTable(params (string label, Control control)[] fields)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = fields.Length, AutoSize = true, Dock = DockStyle.Top,
            Padding = new Padding(0, 4, 0, 4)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < fields.Length; i++)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label
            {
                Text = fields[i].label, AutoSize = true, Anchor = AnchorStyles.Left,
                Font = BoldFont, Margin = new Padding(0, 8, 4, 4)
            }, 0, i);
            fields[i].control.Dock = DockStyle.Fill;
            fields[i].control.Margin = new Padding(0, 4, 0, 4);
            t.Controls.Add(fields[i].control, 1, i);
        }
        return t;
    }

    public static FlowLayoutPanel ButtonRow(params Control[] buttons)
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, Padding = new Padding(0, 8, 0, 8)
        };
        p.Controls.AddRange(buttons);
        return p;
    }

    public static void Error(Exception ex) =>
        MessageBox.Show(Friendly(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static void Warn(string msg) =>
        MessageBox.Show(msg, "Check input", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Info(string msg) =>
        MessageBox.Show(msg, "B2B Wholesale", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(string msg) =>
        MessageBox.Show(msg, "Please confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>Turns database errors into messages an admin can act on.</summary>
    public static string Friendly(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException != null) inner = inner.InnerException;
        var m = inner.Message;
        if (m.Contains("FK_OrderDetail_Item")) return "This item appears in existing orders and cannot be deleted. Set its stock to 0 instead.";
        if (m.Contains("FK_Order_Agent")) return "This agent has order history and cannot be deleted.";
        if (m.Contains("UQ_Users_UserName")) return "That username is already taken.";
        if (m.Contains("UQ_Users_Email")) return "That email is already used by another account.";
        if (m.Contains("IX_Item_Name_Size")) return "An item with the same name and size already exists.";
        if (m.Contains("CK_Item_Stock")) return "Stock quantity cannot become negative.";
        if (m.Contains("network-related") || m.Contains("server was not found"))
            return "Cannot connect to SQL Server. Check the 'WholesaleDb' connection string in App.config.\n\n" + m;
        return m;
    }
}
