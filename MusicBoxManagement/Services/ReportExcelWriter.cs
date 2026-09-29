using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class ReportExcelWriter
    {
        private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static byte[] Write(ReportData report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    AddText(archive, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                        "</Types>");
                    AddText(archive, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "</Relationships>");
                    AddText(archive, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                        "</Relationships>");
                    using (var entry = archive.CreateEntry("xl/workbook.xml").Open())
                    using (var writer = XmlWriter.Create(entry, new XmlWriterSettings { Encoding = Utf8 }))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("workbook", SpreadsheetNs);
                        writer.WriteAttributeString("xmlns", "r", null, OfficeRelNs);
                        writer.WriteStartElement("sheets", SpreadsheetNs);
                        writer.WriteStartElement("sheet", SpreadsheetNs);
                        writer.WriteAttributeString("name", "Báo cáo");
                        writer.WriteAttributeString("sheetId", "1");
                        writer.WriteAttributeString("r", "id", OfficeRelNs, "rId1");
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                    using (var entry = archive.CreateEntry("xl/worksheets/sheet1.xml").Open())
                    using (var writer = XmlWriter.Create(entry, new XmlWriterSettings { Encoding = Utf8 }))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("worksheet", SpreadsheetNs);
                        writer.WriteStartElement("sheetData", SpreadsheetNs);
                        WriteRow(writer, 1, new[] { ReportCell.Text(report.Title) });
                        WriteRow(writer, 2, new[] { ReportCell.Text(report.TimeBasis) });
                        WriteRow(writer, 3, new[] { ReportCell.Text(report.FilterDescription) });
                        WriteRow(writer, 4, new[] { ReportCell.Text("Xuất lúc: " +
                            report.GeneratedAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"))) });
                        var headers = new ReportCell[report.Headers.Count];
                        for (var i = 0; i < headers.Length; i++) headers[i] = ReportCell.Text(report.Headers[i]);
                        WriteRow(writer, 6, headers);
                        var rowNumber = 7;
                        foreach (var row in report.Rows) WriteRow(writer, rowNumber++, row.Cells);
                        if (report.Total != null) WriteRow(writer, rowNumber, report.Total.Cells);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
                return stream.ToArray();
            }
        }

        private static void AddText(ZipArchive archive, string path, string content)
        {
            using (var writer = new StreamWriter(archive.CreateEntry(path).Open(), Utf8))
                writer.Write(content);
        }

        private static void WriteRow(XmlWriter writer, int number, System.Collections.Generic.IEnumerable<ReportCell> cells)
        {
            writer.WriteStartElement("row", SpreadsheetNs);
            writer.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
            var column = 1;
            foreach (var cell in cells)
            {
                writer.WriteStartElement("c", SpreadsheetNs);
                writer.WriteAttributeString("r", ColumnName(column++) + number.ToString(CultureInfo.InvariantCulture));
                if (cell.Number.HasValue)
                {
                    writer.WriteElementString("v", SpreadsheetNs,
                        cell.Number.Value.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", SpreadsheetNs);
                    writer.WriteElementString("t", SpreadsheetNs, cell.Display ?? "");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static string ColumnName(int index)
        {
            var result = "";
            while (index > 0)
            {
                index--;
                result = (char)('A' + index % 26) + result;
                index /= 26;
            }
            return result;
        }
    }
}
