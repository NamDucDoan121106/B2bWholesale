using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using B2BWholesale.WinForms.Infrastructure;
using B2BWholesale.WinForms.Reports;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.WinForms.Forms;

/// <summary>
/// Task 1.2 - Order management: view web orders (master/detail), update OrderStatus.
/// Task 1.3 - Print a Shipping Manifest (PDF) for the selected order.
/// </summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class OrdersForm : Form
{
    private readonly DataGridView _orders = Ui.Grid();
    private readonly DataGridView _lines = Ui.Grid();

    private readonly ComboBox _cboStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly ComboBox _cboAgent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly DateTimePicker _dtFrom = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false };
    private readonly DateTimePicker _dtTo = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false };

    private readonly Label _lblSelected = new() { AutoSize = true, Font = Ui.BoldFont, Margin = new Padding(0, 8, 12, 0) };
    private readonly ComboBox _cboNewStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly Button _btnUpdate = Ui.Btn("Update status", width: 140);
    private readonly Button _btnManifest = Ui.Btn("Shipping manifest (PDF)", Ui.Success, 210);
    private readonly Label _lblTotal = new() { AutoSize = true, Font = Ui.BoldFont, ForeColor = Ui.Primary, Margin = new Padding(16, 8, 0, 0) };

    internal sealed class OrderRow
    {
        public int OrderID { get; init; }
        public DateTime OrderDate { get; init; }
        public string AgentName { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public int Lines { get; init; }
        public int Units { get; init; }
        public decimal Total { get; init; }
    }

    public OrdersForm()
    {
        Text = "Orders";
        BackColor = Color.White;

        _cboStatus.Items.Add("All");
        _cboStatus.Items.AddRange(OrderStatuses.All);
        _cboStatus.SelectedIndex = 0;

        var btnFilter = Ui.Btn("Filter", width: 80);
        var btnRefresh = Ui.Btn("Refresh", Ui.Muted, 90);

        var filter = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        filter.Controls.AddRange(new Control[]
        {
            Lbl("Status"), _cboStatus, Lbl("Agent"), _cboAgent, Lbl("From"), _dtFrom, Lbl("To"), _dtTo, btnFilter, btnRefresh
        });

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 8) };
        actions.Controls.AddRange(new Control[] { _lblSelected, Lbl("Change to"), _cboNewStatus, _btnUpdate, _btnManifest, _lblTotal });

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_orders);
        split.Panel2.Controls.Add(_lines);
        split.Panel2.Controls.Add(actions);

        Controls.Add(split);
        Controls.Add(filter);
        Controls.Add(Ui.Title("Order Management"));

        btnFilter.Click += async (_, _) => await LoadOrdersAsync();
        btnRefresh.Click += async (_, _) => await LoadOrdersAsync(SelectedOrderId);
        _orders.SelectionChanged += async (_, _) => await LoadLinesAsync();
        _orders.CellFormatting += StatusColouring;
        _btnUpdate.Click += async (_, _) => await UpdateStatusAsync();
        _btnManifest.Click += async (_, _) => await ManifestAsync();

        Load += async (_, _) =>
        {
            if (split.Height > 300) split.SplitterDistance = split.Height * 55 / 100;
            await LoadAgentsAsync();
            await LoadOrdersAsync();
        };
    }

    private static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 8, 4, 0) };

    private int? SelectedOrderId => _orders.CurrentRow?.Cells["OrderID"].Value as int?;

    private async Task LoadAgentsAsync()
    {
        try
        {
            await using var db = AppServices.NewDb();
            var agents = await db.Agents.AsNoTracking().OrderBy(a => a.AgentName)
                                 .Select(a => new { a.AgentID, a.AgentName }).ToListAsync();
            agents.Insert(0, new { AgentID = 0, AgentName = "All agents" });
            _cboAgent.DisplayMember = "AgentName";
            _cboAgent.ValueMember = "AgentID";
            _cboAgent.DataSource = agents;
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task LoadOrdersAsync(int? selectId = null)
    {
        try
        {
            await using var db = AppServices.NewDb();
            var q = db.Orders.AsNoTracking().AsQueryable();
            if (_cboStatus.SelectedIndex > 0) { var s = _cboStatus.SelectedItem!.ToString(); q = q.Where(o => o.OrderStatus == s); }
            if (_cboAgent.SelectedValue is int agentId && agentId > 0) q = q.Where(o => o.AgentID == agentId);
            if (_dtFrom.Checked) { var f = _dtFrom.Value.Date; q = q.Where(o => o.OrderDate >= f); }
            if (_dtTo.Checked) { var t = _dtTo.Value.Date.AddDays(1); q = q.Where(o => o.OrderDate < t); }

            var rows = await q.OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderRow
                {
                    OrderID = o.OrderID,
                    OrderDate = o.OrderDate,
                    AgentName = o.Agent!.AgentName,
                    OrderStatus = o.OrderStatus,
                    Lines = o.OrderDetails.Count,
                    Units = o.OrderDetails.Sum(d => (int?)d.Quantity) ?? 0,
                    Total = o.OrderDetails.Sum(d => (decimal?)(d.Quantity * d.UnitAmount)) ?? 0m
                }).ToListAsync();

            _orders.DataSource = rows;
            Ui.Columns(_orders, ("OrderID", "Order #", null, 12), ("OrderDate", "Order date", "dd/MM/yyyy HH:mm", 25),
                ("AgentName", "Agent", null, 55), ("OrderStatus", "Status", null, 18), ("Lines", "Lines", "N0", 10),
                ("Units", "Units", "N0", 12), ("Total", "Total (VND)", "N0", 25));
            _orders.Columns["OrderDate"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            if (selectId.HasValue)
                foreach (DataGridViewRow r in _orders.Rows)
                    if ((int)r.Cells["OrderID"].Value == selectId) { _orders.CurrentCell = r.Cells["AgentName"]; break; }

            if (rows.Count == 0) await LoadLinesAsync();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private void StatusColouring(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _orders.Columns[e.ColumnIndex].Name != "OrderStatus" || e.Value is not string s) return;
        e.CellStyle!.Font = Ui.BoldFont;
        e.CellStyle.ForeColor = s switch
        {
            OrderStatuses.Pending => Color.DarkOrange,
            OrderStatuses.Confirmed => Ui.Primary,
            OrderStatuses.Shipped => Color.SteelBlue,
            OrderStatuses.Delivered => Ui.Success,
            _ => Ui.Muted
        };
    }

    private int _loadVersion;

    private async Task LoadLinesAsync()
    {
        var id = SelectedOrderId;
        var version = ++_loadVersion;
        _cboNewStatus.Items.Clear();
        if (id == null)
        {
            _lines.DataSource = null;
            _lblSelected.Text = "No order selected";
            _lblTotal.Text = "";
            _btnUpdate.Enabled = _btnManifest.Enabled = false;
            return;
        }
        try
        {
            await using var db = AppServices.NewDb();
            var order = await new OrderService(db).GetOrderAsync(id.Value);
            if (version != _loadVersion || order == null) return;   // a newer selection won

            _lines.DataSource = order.OrderDetails.OrderBy(d => d.ID).Select((d, i) => new
            {
                No = i + 1, d.ItemID, ItemName = d.Item!.ItemName, d.Item.Size, d.Quantity, d.UnitAmount, LineTotal = d.LineTotal
            }).ToList();
            Ui.Columns(_lines, ("No", "#", null, 6), ("ItemID", "Item ID", null, 10), ("ItemName", "Item", null, 50),
                ("Size", "Size", null, 35), ("Quantity", "Qty", "N0", 12), ("UnitAmount", "Unit price", "N0", 20),
                ("LineTotal", "Line total", "N0", 22));

            _lblSelected.Text = $"Order #{order.OrderID} - {order.OrderStatus}";
            _lblTotal.Text = $"Total: {Ui.Money(order.TotalAmount)}";
            var next = OrderStatuses.NextStatuses(order.OrderStatus);
            foreach (var s in next) _cboNewStatus.Items.Add(s);
            if (next.Count > 0) _cboNewStatus.SelectedIndex = 0;
            _btnUpdate.Enabled = next.Count > 0;
            _btnManifest.Enabled = order.OrderStatus != OrderStatuses.Cancelled;
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task UpdateStatusAsync()
    {
        if (SelectedOrderId is not int id || _cboNewStatus.SelectedItem is not string status) return;
        var msg = status == OrderStatuses.Cancelled
            ? $"Cancel order #{id}? The ordered quantities will be returned to stock."
            : $"Change order #{id} to '{status}'?";
        if (!Ui.Confirm(msg)) return;
        try
        {
            await using var db = AppServices.NewDb();
            await new OrderService(db).UpdateStatusAsync(id, status);
            await LoadOrdersAsync(id);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task ManifestAsync()
    {
        if (SelectedOrderId is not int id) return;
        try
        {
            await using var db = AppServices.NewDb();
            var order = await new OrderService(db).GetOrderAsync(id);
            if (order == null) return;

            using var dlg = new SaveFileDialog
            {
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"ShippingManifest_Order{order.OrderID:D5}.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            ShippingManifestReport.Generate(order, Session.CurrentUser?.UserName ?? "admin", dlg.FileName);
            if (Ui.Confirm("Shipping manifest saved. Open it now to print?"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
