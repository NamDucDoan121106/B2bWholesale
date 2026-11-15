using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using B2BWholesale.WinForms.Infrastructure;

namespace B2BWholesale.WinForms.Forms;

/// <summary>Task 1.1 - Secure login. Only users with the Admin role may enter.</summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class LoginForm : Form
{
    private readonly TextBox _txtUser = new() { PlaceholderText = "Username or email" };
    private readonly TextBox _txtPass = new() { PlaceholderText = "Password", UseSystemPasswordChar = true };
    private readonly CheckBox _chkShow = new() { Text = "Show password", AutoSize = true };
    private readonly Button _btnLogin = Ui.Btn("Log in", width: 300);
    private readonly Label _lblStatus = new() { ForeColor = Ui.Danger, AutoSize = false, Height = 44, Dock = DockStyle.Top };

    public LoginForm()
    {
        Text = "B2B Wholesale - Back Office Login";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(380, 400);
        BackColor = Color.White;
        Font = Ui.BaseFont;

        var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Ui.Primary };
        header.Controls.Add(new Label
        {
            Text = "B2B Wholesale\nBack-Office (Admin)", ForeColor = Color.White, Font = Ui.TitleFont,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
        });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(40, 20, 40, 10) };
        foreach (var c in new Control[] { _txtUser, _txtPass }) { c.Dock = DockStyle.Top; c.Height = 30; }
        _chkShow.Dock = DockStyle.Top;
        _btnLogin.Dock = DockStyle.Top;
        _btnLogin.Height = 40;

        // Dock=Top stacks in reverse order of adding
        body.Controls.Add(_lblStatus);
        body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10 });
        body.Controls.Add(_btnLogin);
        body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14 });
        body.Controls.Add(_chkShow);
        body.Controls.Add(_txtPass);
        body.Controls.Add(new Label { Text = "Password", Dock = DockStyle.Top, Font = Ui.BoldFont, Height = 26 });
        body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        body.Controls.Add(_txtUser);
        body.Controls.Add(new Label { Text = "Username", Dock = DockStyle.Top, Font = Ui.BoldFont, Height = 26 });

        Controls.Add(body);
        Controls.Add(header);

        AcceptButton = _btnLogin;
        _chkShow.CheckedChanged += (_, _) => _txtPass.UseSystemPasswordChar = !_chkShow.Checked;
        _btnLogin.Click += async (_, _) => await LoginAsync();
    }

    private async Task LoginAsync()
    {
        _lblStatus.Text = "";
        if (string.IsNullOrWhiteSpace(_txtUser.Text) || string.IsNullOrEmpty(_txtPass.Text))
        {
            _lblStatus.Text = "Please enter both username and password.";
            return;
        }

        _btnLogin.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await using var db = AppServices.NewDb();
            var result = await new AuthService(db).LoginAsync(_txtUser.Text, _txtPass.Text, Roles.Admin);
            if (result.Succeeded)
            {
                Session.CurrentUser = result.User;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            _lblStatus.Text = AuthService.Describe(result, "back-office application (Admin only)");
            _txtPass.Clear();
            _txtPass.Focus();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Login failed - see details.";
            Ui.Error(ex);
        }
        finally
        {
            _btnLogin.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
