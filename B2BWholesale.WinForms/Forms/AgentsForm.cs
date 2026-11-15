using System.Text.RegularExpressions;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Security;
using B2BWholesale.WinForms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.WinForms.Forms;

/// <summary>
/// Task 1.2 - Agent CRUD. Registering an agent can also create its Agent-role
/// portal login in the same transaction.
/// </summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public partial class AgentsForm : Form
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _txtSearch = new() { PlaceholderText = "Search name, address or phone...", Width = 320 };

    private readonly TextBox _txtId = new() { ReadOnly = true, PlaceholderText = "(new)" };
    private readonly TextBox _txtName = new() { MaxLength = 150 };
    private readonly TextBox _txtAddress = new() { MaxLength = 300, Multiline = true, Height = 60, MinimumSize = new Size(0, 60) };
    private readonly TextBox _txtPhone = new() { MaxLength = 20, PlaceholderText = "e.g. 0909 315 228" };

    private readonly CheckBox _chkAccount = new() { Text = "Create portal login for this agent", AutoSize = true, Checked = true };
    private readonly TextBox _txtUser = new() { MaxLength = 50 };
    private readonly TextBox _txtEmail = new() { MaxLength = 150 };
    private readonly TextBox _txtPassword = new() { UseSystemPasswordChar = true, PlaceholderText = "min. 8 chars, letters + digits" };
    private readonly Label _lblAccount = new() { AutoSize = true, ForeColor = Ui.Muted, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };

    private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private List<AgentRow> _rows = new();

    internal sealed class AgentRow
    {
        public int AgentID { get; init; }
        public string AgentName { get; init; } = "";
        public string Address { get; init; } = "";
        public string Phone { get; init; } = "";
        public int Orders { get; init; }
        public string Login { get; init; } = "";
    }

    [GeneratedRegex(@"^(\+84|0)[0-9 .\-]{8,13}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public AgentsForm()
    {
        Text = "Agents";
        BackColor = Color.White;

        var btnAdd = Ui.Btn("Register", Ui.Success);
        var btnUpdate = Ui.Btn("Update");
        var btnDelete = Ui.Btn("Delete", Ui.Danger);
        var btnClear = Ui.Btn("Clear", Ui.Muted, 80);

        var accountBox = new GroupBox { Text = "Agent portal account", Dock = DockStyle.Top, Height = 200, Padding = new Padding(10) };
        var accountFields = Ui.FieldTable(("Username", _txtUser), ("Email", _txtEmail), ("Password", _txtPassword));
        accountBox.Controls.Add(accountFields);
        accountBox.Controls.Add(_chkAccount);
        _chkAccount.Dock = DockStyle.Top;

        var edit = new Panel { Dock = DockStyle.Right, Width = 430, Padding = new Padding(16, 0, 0, 0), AutoScroll = true };
        edit.Controls.Add(_lblAccount);
        edit.Controls.Add(accountBox);
        edit.Controls.Add(Ui.ButtonRow(btnAdd, btnUpdate, btnDelete, btnClear));
        edit.Controls.Add(Ui.FieldTable(("Agent ID", _txtId), ("Agent name *", _txtName),
                                        ("Address *", _txtAddress), ("Phone *", _txtPhone)));
        edit.Controls.Add(new Label { Text = "Distributor agent", Font = Ui.BoldFont, Dock = DockStyle.Top, Height = 28 });

        var search = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        search.Controls.Add(_txtSearch);

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_grid);
        left.Controls.Add(search);

        Controls.Add(left);
        Controls.Add(edit);
        Controls.Add(Ui.Title("Distributor Agents"));

        _txtSearch.TextChanged += (_, _) => Bind();
        _grid.SelectionChanged += (_, _) => FillFromSelection();
        _chkAccount.CheckedChanged += (_, _) => ToggleAccountFields();
        btnAdd.Click += async (_, _) => await RegisterAsync();
        btnUpdate.Click += async (_, _) => await UpdateAsync();
        btnDelete.Click += async (_, _) => await DeleteAsync();
        btnClear.Click += (_, _) => ClearFields();

        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(int? selectId = null)
    {
        try
        {
            await using var db = AppServices.NewDb();
            _rows = await db.Agents.AsNoTracking().OrderBy(a => a.AgentName)
                .Select(a => new AgentRow
                {
                    AgentID = a.AgentID, AgentName = a.AgentName, Address = a.Address, Phone = a.Phone,
                    Orders = a.Orders.Count,
                    Login = a.Users.Select(u => u.UserName + (u.IsLocked ? " (locked)" : "")).FirstOrDefault() ?? ""
                }).ToListAsync();
            Bind(selectId);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private void Bind(int? selectId = null)
    {
        var t = _txtSearch.Text.Trim();
        var data = _rows.Where(r => t.Length == 0
                || r.AgentName.Contains(t, StringComparison.OrdinalIgnoreCase)
                || r.Address.Contains(t, StringComparison.OrdinalIgnoreCase)
                || r.Phone.Contains(t)).ToList();
        _grid.DataSource = data;
        Ui.Columns(_grid, ("AgentID", "ID", null, 10), ("AgentName", "Agent name", null, 45),
            ("Address", "Address", null, 80), ("Phone", "Phone", null, 25),
            ("Orders", "Orders", "N0", 14), ("Login", "Portal login", null, 25));
        if (selectId.HasValue)
            foreach (DataGridViewRow r in _grid.Rows)
                if ((int)r.Cells["AgentID"].Value == selectId) { _grid.CurrentCell = r.Cells["AgentName"]; break; }
    }

    private AgentRow? Selected =>
        _grid.CurrentRow?.Cells["AgentID"].Value is int id ? _rows.FirstOrDefault(r => r.AgentID == id) : null;

    private void FillFromSelection()
    {
        var a = Selected;
        if (a == null) return;
        _errors.Clear();
        _txtId.Text = a.AgentID.ToString();
        _txtName.Text = a.AgentName;
        _txtAddress.Text = a.Address;
        _txtPhone.Text = a.Phone;
        _chkAccount.Checked = false;
        _chkAccount.Enabled = a.Login.Length == 0;   // one login per agent
        _lblAccount.Text = a.Login.Length > 0
            ? $"Portal login: {a.Login}. Manage it in 'User accounts'."
            : "This agent has no portal login yet - tick the box and click Update to create one.";
        ToggleAccountFields();
    }

    private void ToggleAccountFields()
    {
        foreach (var c in new Control[] { _txtUser, _txtEmail, _txtPassword }) c.Enabled = _chkAccount.Checked;
    }

    private void ClearFields()
    {
        _grid.ClearSelection();
        _errors.Clear();
        foreach (var t in new[] { _txtId, _txtName, _txtAddress, _txtPhone, _txtUser, _txtEmail, _txtPassword }) t.Clear();
        _chkAccount.Enabled = true;
        _chkAccount.Checked = true;
        _lblAccount.Text = "";
        _txtName.Focus();
    }

    private bool ValidateAgent()
    {
        _errors.Clear();
        var ok = true;
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _errors.SetError(_txtName, "Agent name is required."); ok = false; }
        if (string.IsNullOrWhiteSpace(_txtAddress.Text)) { _errors.SetError(_txtAddress, "Address is required."); ok = false; }
        if (!PhoneRegex().IsMatch(_txtPhone.Text.Trim())) { _errors.SetError(_txtPhone, "Enter a valid Vietnamese phone number."); ok = false; }
        if (_chkAccount.Checked && _chkAccount.Enabled)
        {
            if (_txtUser.Text.Trim().Length < 4) { _errors.SetError(_txtUser, "Username needs at least 4 characters."); ok = false; }
            if (!EmailRegex().IsMatch(_txtEmail.Text.Trim())) { _errors.SetError(_txtEmail, "Enter a valid email."); ok = false; }
            var p = _txtPassword.Text;
            if (p.Length < 8 || !p.Any(char.IsLetter) || !p.Any(char.IsDigit))
            { _errors.SetError(_txtPassword, "At least 8 characters with letters and digits."); ok = false; }
        }
        return ok;
    }

    private User NewLogin(Agent agent) => new()
    {
        UserName = _txtUser.Text.Trim(),
        Email = _txtEmail.Text.Trim(),
        Password = PasswordHasher.Hash(_txtPassword.Text),
        Role = Roles.Agent,
        Agent = agent
    };

    private async Task RegisterAsync()
    {
        if (!ValidateAgent()) return;
        try
        {
            await using var db = AppServices.NewDb();
            var agent = new Agent { AgentName = _txtName.Text.Trim(), Address = _txtAddress.Text.Trim(), Phone = _txtPhone.Text.Trim() };
            db.Agents.Add(agent);
            if (_chkAccount.Checked) db.Users.Add(NewLogin(agent));
            await db.SaveChangesAsync();   // agent + login saved together (single transaction)
            Ui.Info($"Agent #{agent.AgentID} registered" + (_chkAccount.Checked ? $" with login '{_txtUser.Text.Trim()}'." : "."));
            await ReloadAsync(agent.AgentID);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task UpdateAsync()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn("Select an agent to update."); return; }
        if (!ValidateAgent()) return;
        try
        {
            await using var db = AppServices.NewDb();
            var agent = await db.Agents.FindAsync(sel.AgentID) ?? throw new InvalidOperationException("Agent no longer exists.");
            agent.AgentName = _txtName.Text.Trim();
            agent.Address = _txtAddress.Text.Trim();
            agent.Phone = _txtPhone.Text.Trim();
            if (_chkAccount.Checked && _chkAccount.Enabled) db.Users.Add(NewLogin(agent));
            await db.SaveChangesAsync();
            Ui.Info($"Agent #{agent.AgentID} updated.");
            await ReloadAsync(agent.AgentID);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task DeleteAsync()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn("Select an agent to delete."); return; }
        if (sel.Orders > 0) { Ui.Warn($"'{sel.AgentName}' has {sel.Orders} order(s) and cannot be deleted."); return; }
        if (!Ui.Confirm($"Delete agent '{sel.AgentName}'? Its portal login will also be removed.")) return;
        try
        {
            await using var db = AppServices.NewDb();
            await db.Agents.Where(a => a.AgentID == sel.AgentID).ExecuteDeleteAsync();   // Users cascade in DB
            ClearFields();
            await ReloadAsync();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
