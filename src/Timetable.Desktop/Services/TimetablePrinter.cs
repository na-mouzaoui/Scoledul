using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Infrastructure.Export;

namespace Timetable.Desktop.Services;

/// <summary>Impression directe de la vue courante (FR-13) via le pilote d'impression Windows.</summary>
public sealed class TimetablePrinter : ITimetablePrinter
{
    public Task PrintAsync(TimetableDocument document, CancellationToken cancellationToken = default)
    {
        var grids = TimetableGridBuilder.Build(document);
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return Task.CompletedTask;
        }

        var flow = BuildFlowDocument(document, grids, dialog);
        dialog.PrintDocument(((IDocumentPaginatorSource)flow).DocumentPaginator, document.ProjectName);
        return Task.CompletedTask;
    }

    private static FlowDocument BuildFlowDocument(
        TimetableDocument source,
        IReadOnlyList<TimetableGrid> grids,
        PrintDialog dialog)
    {
        var pageWidth = dialog.PrintableAreaWidth;
        var pageHeight = dialog.PrintableAreaHeight;

        var flow = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            ColumnWidth = pageWidth,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9,
            PagePadding = new Thickness(20),
        };

        var firstGrid = true;
        foreach (var grid in grids)
        {
            var title = new Paragraph(new Run($"{grid.Title}  —  {source.ProjectName} / {source.VersionName}"))
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, firstGrid ? 0 : 12, 0, 8),
                BreakPageBefore = !firstGrid,
            };

            flow.Blocks.Add(title);
            flow.Blocks.Add(BuildTable(grid, pageWidth - 40));
            firstGrid = false;
        }

        return flow;
    }

    private static Table BuildTable(TimetableGrid grid, double availableWidth)
    {
        var columnCount = grid.ColumnHeaders.Count + 1;
        var columnWidth = Math.Max(40, availableWidth / Math.Max(1, columnCount));

        var table = new Table { CellSpacing = 0 };
        for (var i = 0; i < columnCount; i++)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(columnWidth) });
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        var header = new TableRow();
        header.Cells.Add(HeaderCell("Créneau"));
        foreach (var column in grid.ColumnHeaders)
        {
            header.Cells.Add(HeaderCell(column));
        }

        group.Rows.Add(header);

        for (var rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
        {
            var row = grid.Rows[rowIndex];
            var tableRow = new TableRow();
            tableRow.Cells.Add(BodyCell(row.IsBreak ? row.Label : row.TimeLabel, null, row.IsBreak, bold: true));

            var cells = grid.Cells[rowIndex];
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                tableRow.Cells.Add(BodyCell(cell.Text ?? string.Empty, cell.ColorHex, cell.IsBreak, bold: false));
            }

            group.Rows.Add(tableRow);
        }

        return table;
    }

    private static TableCell HeaderCell(string text) => new(new Paragraph(new Run(text))
    {
        FontWeight = FontWeights.Bold,
    })
    {
        Background = Brushes.Gainsboro,
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(0.5),
        Padding = new Thickness(3),
    };

    private static TableCell BodyCell(string text, string? colorHex, bool isBreak, bool bold)
    {
        var cell = new TableCell(new Paragraph(new Run(text)) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal })
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(3),
        };

        if (isBreak)
        {
            cell.Background = Brushes.LightGray;
        }
        else if (!string.IsNullOrWhiteSpace(colorHex))
        {
            cell.Background = ParseBrush(colorHex);
        }

        return cell;
    }

    private static Brush ParseBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }
}
