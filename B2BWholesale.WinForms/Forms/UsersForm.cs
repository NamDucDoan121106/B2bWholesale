using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Security;
using B2BWholesale.WinForms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.WinForms.Forms;

/// <summary>Supports the login lockout rule: view accounts, lock/unlock, reset passwords.</summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class UsersForm : Form
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _txtNewPass = new() { UseSystemPasswordChar = true, Width = 200, PlaceholderText = "New password" };

    public UsersForm()
    {
        Text = "Users";
        BackColor = Color.White;

        var btnLock = Ui.Btn("Lock", Ui.Danger, 90);
        var btnUnlock = Ui.Btn("Unlock", Ui.Success, 90);
        var btnReset = Ui.Btn("Reset password", Ui.Warning, 150);

        var bar = Ui.ButtonRow(btnLock, btnUnlock, new Label { Width = 30 }, _txtNewPass, btnReset);
        Controls.Add(_grid);
        Controls.Add(bar);
        Controls.Add(new Label
        {
            Text = $"Accounts are locked automatically after {Core.Services.AuthService.MaxFailedAttempts} wrong passwords. Unlocking resets the counter.",
            Dock = DockStyle.Top, Height = 26, ForeColor = Ui.Muted
        });
        Controls.Add(Ui.Title("User Accounts"));

        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "IsLocked" && e.Value is bool b && b)
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Ui.Danger;
        };
        btnLock.Click += async (_, _) => await SetLockAsync(true);
        btnUnlock.Click += async (_, _) => await SetLockAsync(false);
        btnReset.Click += async (_, _) => await ResetPasswordAsync();
        Load += async (_, _) => await ReloadAsync();
    }

    private int? SelectedId => _grid.CurrentRow?.Cells["UserID"].Value as int?;

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.NewDb();
            _grid.DataSource = await db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.UserName)
                .Select(u => new
                {
                    u.UserID, u.UserName, u.Email, u.Role,
                    Agent = u.Agent != null ? u.Agent.AgentName : "",
                    u.FailedLoginAttempts, u.IsLocked
                }).ToListAsync();
            Ui.Columns(_grid, ("UserID", "ID", null, 8), ("UserName", "Username", null, 20), ("Email", "Email", null, 35),
                ("Role", "Role", null, 12), ("Agent", "Agent", null, 40), ("FailedLoginAttempts", "Failed", "N0", 10),
                ("IsLocked", "Locked", null, 10));
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task SetLockAsync(bool locked)
    {
        if (SelectedId is not int id) return;
        if (locked && id == Session.CurrentUser?.UserID) { Ui.Warn("You cannot lock your own account."); return; }
        try
        {
            await using var db = AppServices.NewDb();
            await db.Users.Where(u => u.UserID == id).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsLocked, locked)
                .SetProperty(u => u.FailedLoginAttempts, 0));
            await ReloadAsync();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task ResetPasswordAsync()
    {
        if (SelectedId is not int id) return;
        var p = _txtNewPass.Text;
        if (p.Length < 8 || !p.Any(char.IsLetter) || !p.Any(char.IsDigit))
        { Ui.Warn("Password needs at least 8 characters with letters and digits."); return; }
        try
        {
            var hash = PasswordHasher.Hash(p);
            await using var db = AppServices.NewDb();
            await db.Users.Where(u => u.UserID == id).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Password, hash)
                .SetProperty(u => u.FailedLoginAttempts, 0)
                .SetProperty(u => u.IsLocked, false));
            _txtNewPass.Clear();
            Ui.Info("Password reset and account unlocked.");
            await ReloadAsync();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
