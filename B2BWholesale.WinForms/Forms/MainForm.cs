using B2BWholesale.WinForms.Infrastructure;

namespace B2BWholesale.WinForms.Forms;

/// <summary>Shell window: left navigation, child screens are hosted in the content panel.</summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class MainForm : Form
{
    private readonly Panel _content = new() { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Color.White };
    private readonly List<Button> _navButtons = new();
    private Form? _current;

    public bool LoggedOut { get; private set; }

    public MainForm()
    {
        Text = "B2B Wholesale Management - Back Office";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1150, 700);
        Font = Ui.BaseFont;

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 210, BackColor = Ui.Primary, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(0, 10, 0, 0)
        };
        nav.Controls.Add(new Label
        {
            Text = "B2B WHOLESALE", ForeColor = Color.White, Font = Ui.TitleFont, AutoSize = false,
            Width = 210, Height = 44, TextAlign = ContentAlignment.MiddleCenter
        });
        nav.Controls.Add(new Label
        {
            Text = $"Signed in: {Session.CurrentUser?.UserName}", ForeColor = Color.Gainsboro,
            AutoSize = false, Width = 210, Height = 30, TextAlign = ContentAlignment.MiddleCenter
        });

        AddNav(nav, "Dashboard", () => new DashboardForm());
        AddNav(nav, "Items / Stock", () => new ItemsForm());
        AddNav(nav, "Agents", () => new AgentsForm());
        AddNav(nav, "Orders", () => new OrdersForm());
        AddNav(nav, "User accounts", () => new UsersForm());

        var logout = NavButton("Log out");
        logout.Click += (_, _) =>
        {
            if (!Ui.Confirm("Log out of the back office?")) return;
            LoggedOut = true;
            Session.CurrentUser = null;
            Close();
        };
        nav.Controls.Add(new Panel { Width = 210, Height = 30 });
        nav.Controls.Add(logout);

        Controls.Add(_content);
        Controls.Add(nav);

        Shown += (_, _) => _navButtons[0].PerformClick();
    }

    private Button NavButton(string text)
    {
        var b = new Button
        {
            Text = text, Width = 210, Height = 46, FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Ui.Primary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 0, 0),
            Font = Ui.BoldFont, Margin = new Padding(0), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 98, 145);
        return b;
    }

    private void AddNav(FlowLayoutPanel nav, string text, Func<Form> factory)
    {
        var b = NavButton(text);
        b.Click += (_, _) =>
        {
            foreach (var other in _navButtons) other.BackColor = Ui.Primary;
            b.BackColor = Color.FromArgb(60, 120, 170);
            ShowChild(factory());
        };
        _navButtons.Add(b);
        nav.Controls.Add(b);
    }

    private void ShowChild(Form child)
    {
        _current?.Close();
        _current?.Dispose();
        _current = child;
        child.TopLevel = false;
        child.FormBorderStyle = FormBorderStyle.None;
        child.Dock = DockStyle.Fill;
        _content.Controls.Clear();
        _content.Controls.Add(child);
        child.Show();
    }
}
