using B2BWholesale.Core.Entities;
using B2BWholesale.WinForms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.WinForms.Forms;

/// <summary>Task 1.2 - Item CRUD and stock updates (restock / adjust).</summary>
[System.ComponentModel.DesignerCategory("Code")]   // UI is built in code - opens in the code editor
public class ItemsForm : Form
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _txtSearch = new() { PlaceholderText = "Search by name or size...", Width = 300 };
    private readonly CheckBox _chkLow = new() { Text = "Only low stock", AutoSize = true, Margin = new Padding(12, 6, 0, 0) };
    private readonly Label _lblCount = new() { AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(12, 8, 0, 0) };

    private readonly TextBox _txtId = new() { ReadOnly = true, PlaceholderText = "(new)" };
    private readonly TextBox _txtName = new() { MaxLength = 150 };
    private readonly TextBox _txtSize = new() { MaxLength = 100, PlaceholderText = "e.g. Carton 24 x 330ml" };
    private readonly NumericUpDown _numPrice = new() { Maximum = 999_999_999, DecimalPlaces = 0, ThousandsSeparator = true, Increment = 1000 };
    private readonly NumericUpDown _numStock = new() { Maximum = 1_000_000, ThousandsSeparator = true };
    private readonly NumericUpDown _numAdjust = new() { Minimum = -1_000_000, Maximum = 1_000_000, ThousandsSeparator = true, Width = 120 };

    private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private List<Item> _items = new();

    public ItemsForm()
    {
        Text = "Items";
        BackColor = Color.White;

        var btnAdd = Ui.Btn("Add new", Ui.Success);
        var btnUpdate = Ui.Btn("Update");
        var btnDelete = Ui.Btn("Delete", Ui.Danger);
        var btnClear = Ui.Btn("Clear", Ui.Muted, 80);
        var btnAdjust = Ui.Btn("Apply", Ui.Warning, 80);

        // ----- right-hand edit panel -----
        var edit = new Panel { Dock = DockStyle.Right, Width = 400, Padding = new Padding(16, 0, 0, 0) };
        var adjustRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        adjustRow.Controls.Add(_numAdjust);
        adjustRow.Controls.Add(btnAdjust);
        var adjustBox = new GroupBox { Text = "Stock movement (+ restock / - write-off)", Dock = DockStyle.Top, Height = 80, Padding = new Padding(10) };
        adjustBox.Controls.Add(adjustRow);

        edit.Controls.Add(adjustBox);
        edit.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
        edit.Controls.Add(Ui.ButtonRow(btnAdd, btnUpdate, btnDelete, btnClear));
        edit.Controls.Add(Ui.FieldTable(
            ("Item ID", _txtId), ("Item name *", _txtName), ("Size *", _txtSize),
            ("Price (VND) *", _numPrice), ("Stock qty *", _numStock)));
        edit.Controls.Add(new Label { Text = "Item details", Font = Ui.BoldFont, Dock = DockStyle.Top, Height = 28 });

        // ----- top search bar -----
        var search = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        search.Controls.AddRange(new Control[] { _txtSearch, _chkLow, _lblCount });

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_grid);
        left.Controls.Add(search);

        Controls.Add(left);
        Controls.Add(edit);
        Controls.Add(Ui.Title("Items & Stock"));

        _txtSearch.TextChanged += (_, _) => Bind();
        _chkLow.CheckedChanged += (_, _) => Bind();
        _grid.SelectionChanged += (_, _) => FillFromSelection();
        _grid.CellFormatting += Grid_CellFormatting;
        btnAdd.Click += async (_, _) => await SaveAsync(isNew: true);
        btnUpdate.Click += async (_, _) => await SaveAsync(isNew: false);
        btnDelete.Click += async (_, _) => await DeleteAsync();
        btnClear.Click += (_, _) => ClearFields();
        btnAdjust.Click += async (_, _) => await AdjustStockAsync();

        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(int? selectId = null)
    {
        try
        {
            await using var db = AppServices.NewDb();
            _items = await db.Items.AsNoTracking().OrderBy(i => i.ItemName).ToListAsync();
            Bind(selectId);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private void Bind(int? selectId = null)
    {
        var term = _txtSearch.Text.Trim();
        var rows = _items.Where(i =>
                (term.Length == 0 || i.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                                  || i.Size.Contains(term, StringComparison.OrdinalIgnoreCase))
                && (!_chkLow.Checked || i.StockQuantity <= AppServices.LowStockThreshold))
            .Select(i => new { i.ItemID, i.ItemName, i.Size, i.Price, i.StockQuantity })
            .ToList();
        _grid.DataSource = rows;
        Ui.Columns(_grid,
            ("ItemID", "ID", null, 12), ("ItemName", "Item name", null, 70), ("Size", "Size", null, 50),
            ("Price", "Price (VND)", "N0", 30), ("StockQuantity", "Stock", "N0", 20));
        _lblCount.Text = $"{rows.Count} item(s) - low-stock threshold: {AppServices.LowStockThreshold}";

        if (selectId.HasValue)
            foreach (DataGridViewRow r in _grid.Rows)
                if ((int)r.Cells["ItemID"].Value == selectId.Value) { r.Selected = true; _grid.CurrentCell = r.Cells["ItemName"]; break; }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "StockQuantity" || e.Value is not int stock) return;
        if (stock <= AppServices.LowStockThreshold)
        {
            e.CellStyle!.ForeColor = Ui.Danger;
            e.CellStyle.Font = Ui.BoldFont;
        }
    }

    private Item? Selected =>
        _grid.CurrentRow?.Cells["ItemID"].Value is int id ? _items.FirstOrDefault(i => i.ItemID == id) : null;

    private void FillFromSelection()
    {
        var item = Selected;
        if (item == null) return;
        _errors.Clear();
        _txtId.Text = item.ItemID.ToString();
        _txtName.Text = item.ItemName;
        _txtSize.Text = item.Size;
        _numPrice.Value = Math.Min(_numPrice.Maximum, item.Price);
        _numStock.Value = Math.Min(_numStock.Maximum, item.StockQuantity);
        _numAdjust.Value = 0;
    }

    private void ClearFields()
    {
        _grid.ClearSelection();
        _errors.Clear();
        _txtId.Clear(); _txtName.Clear(); _txtSize.Clear();
        _numPrice.Value = 0; _numStock.Value = 0; _numAdjust.Value = 0;
        _txtName.Focus();
    }

    private bool ValidateFields()
    {
        _errors.Clear();
        var ok = true;
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _errors.SetError(_txtName, "Item name is required."); ok = false; }
        if (string.IsNullOrWhiteSpace(_txtSize.Text)) { _errors.SetError(_txtSize, "Size / packaging is required."); ok = false; }
        if (_numPrice.Value <= 0) { _errors.SetError(_numPrice, "Price must be greater than 0."); ok = false; }
        return ok;
    }

    private async Task SaveAsync(bool isNew)
    {
        if (!isNew && Selected == null) { Ui.Warn("Select an item to update."); return; }
        if (!ValidateFields()) return;
        try
        {
            await using var db = AppServices.NewDb();
            Item item;
            if (isNew)
            {
                item = new Item();
                db.Items.Add(item);
            }
            else
            {
                item = await db.Items.FindAsync(Selected!.ItemID) ?? throw new InvalidOperationException("Item no longer exists.");
            }
            item.ItemName = _txtName.Text.Trim();
            item.Size = _txtSize.Text.Trim();
            item.Price = _numPrice.Value;
            item.StockQuantity = (int)_numStock.Value;
            await db.SaveChangesAsync();
            Ui.Info(isNew ? $"Item #{item.ItemID} added." : $"Item #{item.ItemID} updated.");
            await ReloadAsync(item.ItemID);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    private async Task DeleteAsync()
    {
        var item = Selected;
        if (item == null) { Ui.Warn("Select an item to delete."); return; }
        if (!Ui.Confirm($"Delete item '{item.ItemName}'?")) return;
        try
        {
            await using var db = AppServices.NewDb();
            var affected = await db.Items.Where(i => i.ItemID == item.ItemID).ExecuteDeleteAsync();
            Ui.Info(affected == 1 ? "Item deleted." : "Item was already removed.");
            ClearFields();
            await ReloadAsync();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    /// <summary>Atomic stock change: UPDATE ... SET Stock = Stock + @delta WHERE Stock + @delta >= 0.</summary>
    private async Task AdjustStockAsync()
    {
        var item = Selected;
        var delta = (int)_numAdjust.Value;
        if (item == null) { Ui.Warn("Select an item first."); return; }
        if (delta == 0) { Ui.Warn("Enter a positive (restock) or negative (write-off) quantity."); return; }
        try
        {
            await using var db = AppServices.NewDb();
            var affected = await db.Items
                .Where(i => i.ItemID == item.ItemID && i.StockQuantity + delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockQuantity, i => i.StockQuantity + delta));
            if (affected == 0) { Ui.Warn("Not enough stock for that write-off."); return; }
            await ReloadAsync(item.ItemID);
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
