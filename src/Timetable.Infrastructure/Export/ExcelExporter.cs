using ClosedXML.Excel;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;

namespace Timetable.Infrastructure.Export;

/// <summary>Export Excel : une feuille par classe ou par enseignant (FR-13).</summary>
public sealed class ExcelExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Excel;

    public string FileExtension => ".xlsx";

    public string FilterDescription => "Classeur Excel (*.xlsx)|*.xlsx";

    public async Task ExportAsync(
        TimetableDocument document,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var grids = TimetableGridBuilder.Build(document);

        await Task.Run(
            () =>
            {
                using var workbook = new XLWorkbook();
                foreach (var grid in grids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteGrid(workbook, grid);
                }

                workbook.SaveAs(destinationPath);
            },
            cancellationToken);
    }

    private static void WriteGrid(XLWorkbook workbook, TimetableGrid grid)
    {
        var worksheet = workbook.Worksheets.Add(SafeSheetName(grid.Title));

        worksheet.Cell(1, 1).Value = grid.Title;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, grid.ColumnHeaders.Count + 1).Merge();

        worksheet.Cell(2, 1).Value = "Créneau";
        for (var column = 0; column < grid.ColumnHeaders.Count; column++)
        {
            worksheet.Cell(2, column + 2).Value = grid.ColumnHeaders[column];
        }

        var headerRange = worksheet.Range(2, 1, 2, grid.ColumnHeaders.Count + 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");

        for (var rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
        {
            var row = grid.Rows[rowIndex];
            var excelRow = rowIndex + 3;

            worksheet.Cell(excelRow, 1).Value = row.IsBreak ? row.Label : row.TimeLabel;
            worksheet.Cell(excelRow, 1).Style.Font.Bold = true;

            var cells = grid.Cells[rowIndex];
            for (var column = 0; column < cells.Count; column++)
            {
                var cell = cells[column];
                var target = worksheet.Cell(excelRow, column + 2);
                target.Value = cell.Text ?? string.Empty;
                target.Style.Alignment.WrapText = true;
                target.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                if (cell.IsBreak)
                {
                    target.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
                    target.Style.Font.Italic = true;
                }
                else if (cell.IsUnavailable)
                {
                    target.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                }
                else if (!string.IsNullOrEmpty(cell.ColorHex))
                {
                    target.Style.Fill.BackgroundColor = XLColor.FromHtml(Normalize(cell.ColorHex));
                }
            }

            if (row.IsBreak)
            {
                worksheet.Range(excelRow, 1, excelRow, grid.ColumnHeaders.Count + 1)
                    .Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            }
        }

        worksheet.Column(1).Width = 14;
        for (var column = 2; column <= grid.ColumnHeaders.Count + 1; column++)
        {
            worksheet.Column(column).Width = 22;
        }

        worksheet.Rows().Height = 40;
    }

    private static string Normalize(string hex)
        => hex.StartsWith('#') ? hex : "#" + hex;

    private static string SafeSheetName(string name)
    {
        foreach (var invalid in new[] { '\\', '/', '?', '*', '[', ']', ':' })
        {
            name = name.Replace(invalid, '-');
        }

        return name.Length <= 31 ? name : name[..31];
    }
}
