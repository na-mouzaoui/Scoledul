using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;

namespace Timetable.Infrastructure.Export;

/// <summary>Export PDF : une page (paysage) par classe ou par enseignant (FR-13).</summary>
public sealed class PdfExporter : IExporter
{
    private const double TotalWidthCentimeters = 26.0;

    static PdfExporter()
    {
        // PDFsharp 6 n'utilise plus les polices système par défaut : on les autorise sous Windows.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public string FileExtension => ".pdf";

    public string FilterDescription => "Document PDF (*.pdf)|*.pdf";

    public async Task ExportAsync(
        TimetableDocument document,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var grids = TimetableGridBuilder.Build(document);

        await Task.Run(
            () =>
            {
                var pdfDocument = BuildDocument(document, grids, cancellationToken);
                pdfDocument.Save(destinationPath);
            },
            cancellationToken);
    }

    private static PdfSharp.Pdf.PdfDocument BuildDocument(
        TimetableDocument document,
        IReadOnlyList<TimetableGrid> grids,
        CancellationToken cancellationToken)
    {
        var migraDocument = new Document();

        var normal = migraDocument.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 8;

        foreach (var grid in grids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddSection(migraDocument, document, grid);
        }

        var renderer = new PdfDocumentRenderer { Document = migraDocument };
        renderer.RenderDocument();
        return renderer.PdfDocument;
    }

    private static void AddSection(Document document, TimetableDocument source, TimetableGrid grid)
    {
        var section = document.AddSection();
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.0);

        var title = section.AddParagraph(grid.Title);
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = "4pt";

        var subtitle = section.AddParagraph(
            $"{source.ProjectName} — {source.VersionName}");
        subtitle.Format.Font.Size = 9;
        subtitle.Format.Font.Italic = true;
        subtitle.Format.SpaceAfter = "8pt";

        var columnCount = grid.ColumnHeaders.Count + 1;
        var columnWidth = Unit.FromCentimeter(TotalWidthCentimeters / Math.Max(1, columnCount));

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;

        table.AddColumn(columnWidth);
        for (var i = 0; i < grid.ColumnHeaders.Count; i++)
        {
            table.AddColumn(columnWidth);
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.LightGray;
        header.Cells[0].AddParagraph("Créneau");
        for (var column = 0; column < grid.ColumnHeaders.Count; column++)
        {
            header.Cells[column + 1].AddParagraph(grid.ColumnHeaders[column]);
        }

        for (var rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
        {
            var row = grid.Rows[rowIndex];
            var tableRow = table.AddRow();

            var labelParagraph = tableRow.Cells[0].AddParagraph(row.IsBreak ? row.Label : row.TimeLabel);
            labelParagraph.Format.Font.Bold = true;

            var cells = grid.Cells[rowIndex];
            for (var column = 0; column < cells.Count; column++)
            {
                var cell = cells[column];
                var tableCell = tableRow.Cells[column + 1];

                if (!string.IsNullOrEmpty(cell.Text))
                {
                    tableCell.AddParagraph(cell.Text);
                }

                if (cell.IsBreak)
                {
                    tableCell.Shading.Color = Colors.LightGray;
                }
                else if (!string.IsNullOrEmpty(cell.ColorHex))
                {
                    tableCell.Shading.Color = ParseColor(cell.ColorHex);
                }
            }
        }
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return Colors.White;
        }

        var red = (byte)((value >> 16) & 0xFF);
        var green = (byte)((value >> 8) & 0xFF);
        var blue = (byte)(value & 0xFF);
        return Color.FromArgb(0xFF, red, green, blue);
    }
}
