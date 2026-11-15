using B2BWholesale.Core.Services;
using B2BWholesale.WinForms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.WinForms.Forms;

/// <summary>
/// Task 1.4 - Data filter / dashboard:
///  - best-selling items (top N, date range)
///  - low-stock warning (configurable threshold)
///  - total revenue by agent (one agent or all, date range)
/// </summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class DashboardForm : Form
{
    private readonly DateTimePicker _dtFrom = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false };
    private readonly DateTimePicker _dtTo = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false };
    private readonly ComboBox _cboAgent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly NumericUpDown _numTop = new() { Minimum = 1, Maximum = 50, Value = 10, Width = 60 };
    private readonly NumericUpDown _numThreshold = new() { Minimum = 1, Maximum = 100_000, Width = 80 };

    private readonly DataGridView _gridBest = Ui.Grid();
    private readonly DataGridView _gridLow = Ui.Grid();
    private readonly DataGridView _gridAgents = Ui.Grid();
    private readonly Label _lblAgentTotal = new() { Dock = DockStyle.Bottom, Height = 34, Font = Ui.BoldFont, ForeColor = Ui.Primary, TextAlign = ContentAlignment.MiddleRight };

    private readonly Label _kRevenue = new(), _kOrders = new(), _kPending = new(), _kLow = new();

    public DashboardForm()
    {
        Text = "Dashboard";
        BackColor = Color.White;
        _numThreshold.Value = AppServices.LowStockThreshold;

        // KPI cards
        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, ColumnCount = 4, Padding = new Padding(0, 0, 0, 8) };
        for (var i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpis.Controls.Add(Card("Revenue (excl. cancelled)", _kRevenue, Ui.Primary), 0, 0);
        kpis.Controls.Add(Card("Orders", _kOrders, Color.SteelBlue), 1, 0);
        kpis.Controls.Add(Card("Pending orders", _kPending, Color.DarkOrange), 2, 0);
        kpis.Controls.Add(Card("Low-stock items", _kLow, Ui.Danger), 3, 0);

        // Filters
        var btnRun = Ui.Btn("Apply filters", width: 130);
        var btnCsv = Ui.Btn("Export tab (CSV)", Ui.Muted, 150);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        filters.Controls.AddRange(new Control[]
        {
            Lbl("From"), _dtFrom, Lbl("To"), _dtTo, Lbl("Agent"), _cboAgent,
            Lbl("Top"), _numTop, Lbl("Low-stock <="), _numThreshold, btnRun, btnCsv
        });

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.BoldFont, Padding = new Point(14, 6) };
        tabs.TabPages.Add(Page("Best-selling items", _gridBest));
        tabs.TabPages.Add(Page("Low-stock warning", _gridLow));
        var agentPage = Page("Revenue by agent", _gridAgents);
        agentPage.Controls.Add(_lblAgentTotal);
        tabs.TabPages.Add(agentPage);

        Controls.Add(tabs);
        Controls.Add(filters);
        Controls.Add(kpis);
        Controls.Add(Ui.Title("Sales Dashboard"));

        _gridLow.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || _gridLow.Columns[e.ColumnIndex].Name != "Level" || e.Value is not string lvl) return;
            e.CellStyle!.Font = Ui.BoldFont;
            e.CellStyle.ForeColor = lvl == "LOW" ? Color.DarkOrange : Ui.Danger;
        };
        btnRun.Click += async (_, _) => await RunAsync();
        btnCsv.Click += (_, _) => ExportCsv(tabs.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault(), tabs.SelectedTab?.Text ?? "export");

        Load += async (_, _) => { await LoadAgentsAsync(); await RunAsync(); };
    }

    private static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 8, 4, 0) };

    private static TabPage Page(string title, DataGridView grid)
    {
        var p = new TabPage(title) { Padding = new Padding(8), BackColor = Color.White };
        grid.Font = Ui.BaseFont;
        p.Controls.Add(grid);
        return p;
    }

    private static Panel Card(string caption, Label value, Color accent)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(14, 8, 8, 8) };
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent });
        value.Dock = DockStyle.Fill;
        value.Font = new Font("Segoe UI Semibold", 18F);
        value.ForeColor = accent;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Padding = new Padding(10, 0, 0, 0);
        card.Controls.Add(value);
        card.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 22, ForeColor = Ui.Muted, Padding = new Padding(10, 0, 0, 0) });
        value.BringToFront();
        return card;
    }

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

    private async Task RunAsync()
    {
        DateTime? from = _dtFrom.Checked ? _dtFrom.Value.Date : null;
        DateTime? to = _dtTo.Checked ? _dtTo.Value.Date : null;
        if (from > to) { Ui.Warn("'From' date must be before 'To' date."); return; }
        int? agentId = _cboAgent.SelectedValue is int id && id > 0 ? id : null;
        var threshold = (int)_numThreshold.Value;

        UseWaitCursor = true;
        try
        {
            await using var db = AppServices.NewDb();
            var svc = new DashboardService(db);

            var summary = await svc.GetSummaryAsync(threshold, from, to);
            _kRevenue.Text = Ui.Money(summary.TotalRevenue);
            _kOrders.Text = summary.TotalOrders.ToString("N0");
            _kPending.Text = summary.PendingOrders.ToString("N0");
            _kLow.Text = summary.LowStockItems.ToString("N0");

            _gridBest.DataSource = await svc.GetBestSellersAsync((int)_numTop.Value, from, to);
            Ui.Columns(_gridBest, ("Rank", "Rank", null, 8), ("ItemID", "Item ID", null, 10), ("ItemName", "Item", null, 50),
                ("Size", "Size", null, 35), ("QuantitySold", "Units sold", "N0", 15), ("Revenue", "Revenue (VND)", "N0", 22),
                ("StockLeft", "Stock left", "N0", 14));

            _gridLow.DataSource = await svc.GetLowStockAsync(threshold);
            Ui.Columns(_gridLow, ("ItemID", "Item ID", null, 10), ("ItemName", "Item", null, 55), ("Size", "Size", null, 40),
                ("StockQuantity", "In stock", "N0", 15), ("Level", "Warning", null, 20));

            var agents = await svc.GetRevenueByAgentAsync(agentId, from, to);
            _gridAgents.DataSource = agents;
            Ui.Columns(_gridAgents, ("AgentID", "Agent ID", null, 10), ("AgentName", "Agent", null, 60),
                ("OrderCount", "Orders", "N0", 12), ("UnitsBought", "Units", "N0", 12), ("Revenue", "Revenue (VND)", "N0", 25));
            _lblAgentTotal.Text = $"Total for selection: {Ui.Money(agents.Sum(a => a.Revenue))}   ";
        }
        catch (Exception ex) { Ui.Error(ex); }
        finally { UseWaitCursor = false; }
    }

    private static void ExportCsv(DataGridView? grid, string name)
    {
        if (grid == null || grid.Rows.Count == 0) { Ui.Warn("Nothing to export on this tab."); return; }
        using var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = name.Replace(' ', '_') + ".csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        static string Esc(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => Esc(c.HeaderText))));
        foreach (DataGridViewRow r in grid.Rows)
            sb.AppendLine(string.Join(",", cols.Select(c => Esc(r.Cells[c.Index].Value))));
        File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        Ui.Info("Exported to " + dlg.FileName);
    }
}
