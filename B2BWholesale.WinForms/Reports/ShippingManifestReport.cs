using B2BWholesale.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace B2BWholesale.WinForms.Reports;

/// <summary>Task 1.3 - "Shipping Manifest" PDF for one order (QuestPDF, Community licence).</summary>
public static class ShippingManifestReport
{
    private static readonly string Navy = "#1F4E79";

    public static void Generate(Order order, string preparedBy, string path)
    {
        var lines = order.OrderDetails.OrderBy(d => d.ID).ToList();
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        string N(decimal v) => v.ToString("N0", vn);

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("B2B WHOLESALE DISTRIBUTION").FontSize(16).Bold().FontColor(Navy);
                            c.Item().Text("Central Warehouse - 19 Nguyen Huu Tho, Tan Phong Ward, District 7, HCMC");
                            c.Item().Text("Hotline: 1900 6868   |   warehouse@b2bwholesale.vn");
                        });
                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("SHIPPING MANIFEST").FontSize(14).Bold();
                            c.Item().AlignRight().Text($"Manifest No: SM-{order.OrderID:D5}");
                            c.Item().AlignRight().Text($"Printed: {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(1.5f).LineColor(Navy);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                        {
                            c.Item().Text("SHIP TO").Bold().FontColor(Navy);
                            c.Item().Text(order.Agent?.AgentName ?? "-").Bold();
                            c.Item().Text(order.Agent?.Address ?? "-");
                            c.Item().Text($"Phone: {order.Agent?.Phone}");
                            c.Item().Text($"Agent ID: {order.AgentID}");
                        });
                        row.ConstantItem(12);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                        {
                            c.Item().Text("ORDER").Bold().FontColor(Navy);
                            c.Item().Text($"Order #: {order.OrderID}");
                            c.Item().Text($"Order date: {order.OrderDate:dd/MM/yyyy HH:mm}");
                            c.Item().Text($"Status: {order.OrderStatus}");
                            c.Item().Text($"Lines: {lines.Count}   |   Total units: {lines.Sum(l => l.Quantity):N0}");
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.ConstantColumn(50);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.ConstantColumn(50);
                            c.ConstantColumn(75);
                            c.ConstantColumn(85);
                            c.ConstantColumn(45);
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "#", "Item ID", "Item", "Packing / size", "Qty", "Unit price", "Amount", "Picked" })
                                h.Cell().Background(Navy).Padding(4).Text(title).FontColor(Colors.White).Bold();
                        });

                        var i = 0;
                        foreach (var d in lines)
                        {
                            i++;
                            var bg = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(4).Text(i.ToString());
                            table.Cell().Background(bg).Padding(4).Text(d.ItemID.ToString());
                            table.Cell().Background(bg).Padding(4).Text(d.Item?.ItemName ?? "");
                            table.Cell().Background(bg).Padding(4).Text(d.Item?.Size ?? "");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(d.Quantity.ToString("N0"));
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(N(d.UnitAmount));
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(N(d.LineTotal));
                            table.Cell().Background(bg).Padding(4).AlignCenter().Text("[   ]");
                        }

                        table.Cell().ColumnSpan(6).BorderTop(1).Padding(4).AlignRight().Text("TOTAL (VND)").Bold();
                        table.Cell().ColumnSpan(2).BorderTop(1).Padding(4).AlignLeft().Text(N(order.TotalAmount)).Bold();
                    });

                    col.Item().PaddingTop(6).Text("Notes: Check quantity and packaging on delivery. Report damaged goods within 24 hours.")
                       .Italic().FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(20).Row(row =>
                    {
                        foreach (var role in new[] { $"Prepared by ({preparedBy})", "Warehouse keeper", "Driver / Carrier", "Received by (Agent)" })
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text(role).Bold();
                                c.Item().AlignCenter().Text("(signature & full name)").FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().Height(50);
                                c.Item().PaddingHorizontal(10).LineHorizontal(0.5f);
                            });
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Shipping manifest for order #" + order.OrderID + "  -  page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);
    }
}
