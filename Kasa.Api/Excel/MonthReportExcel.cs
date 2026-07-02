using System.Globalization;
using ClosedXML.Excel;
using Kasa.Api.Contracts;
using Kasa.Domain;

namespace Kasa.Api.Excel;

/// <summary>Sheet2 "İşlemler" ham satırı: LoadRowsAsync çıktısının dizgiye giden kopyası, hesap yok.</summary>
public record MonthTxnRow(
    DateOnly Date,
    TransactionType Type,
    string Category,
    PaymentMethod PaymentMethod,
    long AmountSatang,
    string? Note);

/// <summary>
/// Aylık raporun BKKBIKE standart stilinde Excel dizgisi. Hesap yapılmaz (I1/I2):
/// month raporu, filo özeti ve ham işlem listesi hazır DTO'lardan hücrelere yazılır.
/// Satang → baht çevirisi SERVER'da bu sınıfta yapılır; hücreler SAYI değerdir, SUM gibi
/// formül yazılmaz — dosya açıldığında görülen her değer API JSON'uyla birebir aynıdır.
/// Dağıtım satırları negatif bakiyede de yazılır (ham veri); gizleme UI kuralıdır.
/// </summary>
public static class MonthReportExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly XLColor Navy = XLColor.FromHtml("#1F3864");
    private static readonly XLColor RowAlt = XLColor.FromHtml("#F5F8FF");

    private const string MoneyFormat = "#,##0.00 ฿";
    private const string PercentFormat = "0.0";
    private const string DateFormat = "dd.MM.yyyy";

    public static byte[] Render(
        MonthReportResponse report, FleetMonthResponse fleet, IReadOnlyList<MonthTxnRow> txns)
    {
        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Tahoma";
        wb.Style.Font.FontSize = 10;

        RenderSummary(wb.AddWorksheet("Ay Özeti"), report, fleet);
        RenderTransactions(wb.AddWorksheet("İşlemler"), txns);
        RenderCategories(wb.AddWorksheet("Kategori Dağılımı"), report);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    // ── Sheet1 "Ay Özeti": gün tablosu + toplam satırı + dağıtım bloğu + filo özeti ────────

    private static void RenderSummary(IXLWorksheet ws, MonthReportResponse r, FleetMonthResponse fleet)
    {
        WriteHeader(ws, ["Tarih", "Gelir", "Gider", "POS", "Gün Net", "Kümülatif Kasa", "Kiralama %"]);

        var rentalByDate = fleet.Days.ToDictionary(d => d.Date, d => d.RentalPercent);

        var row = 2;
        foreach (var day in r.Days)
        {
            DateCell(ws.Cell(row, 1), day.Date);
            MoneyCell(ws.Cell(row, 2), day.IncomeTotal);
            MoneyCell(ws.Cell(row, 3), day.ExpenseTotal);
            MoneyCell(ws.Cell(row, 4), day.PosFee);
            MoneyCell(ws.Cell(row, 5), day.DayNet);
            MoneyCell(ws.Cell(row, 6), day.CumulativeBalance);
            RentalCell(ws.Cell(row, 7), rentalByDate.GetValueOrDefault(day.Date));
            StripeRow(ws, row, 7);
            row++;
        }

        ws.Range(1, 1, row - 1, 7).SetAutoFilter();

        // Toplam satırı: değerler API'nin Totals/FinalBalance alanlarından, burada toplanmaz.
        ws.Cell(row, 1).Value = "TOPLAM";
        MoneyCell(ws.Cell(row, 2), r.Totals.IncomeTotal);
        MoneyCell(ws.Cell(row, 3), r.Totals.ExpenseTotal);
        MoneyCell(ws.Cell(row, 4), r.Totals.PosFee);
        MoneyCell(ws.Cell(row, 5), r.Totals.DayNet);
        MoneyCell(ws.Cell(row, 6), r.FinalBalance);
        var totalRange = ws.Range(row, 1, row, 7);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Double;
        totalRange.Style.Border.TopBorderColor = Navy;

        // Ortaklık dağıtımı bloğu (3 satır) — isim ve yüzdeler DTO'dan, hardcode yok.
        var dist = row + 2;
        LabelMoneyRow(ws, dist, "Ay Sonu Ana Kasa", r.FinalBalance, bold: true);
        LabelMoneyRow(ws, dist + 1, PartnerLabel(r.Distribution.Partner1), r.Distribution.Partner1.AmountSatang);
        LabelMoneyRow(ws, dist + 2, PartnerLabel(r.Distribution.Partner2), r.Distribution.Partner2.AmountSatang);

        // Filo ay özeti (1 satır).
        var f = dist + 4;
        ws.Cell(f, 1).Value = "Ortalama Kiralama %";
        RentalCell(ws.Cell(f, 2), fleet.Summary.AvgRentalPercent);
        ws.Cell(f, 3).Value = "Toplam Arızalı-Gün";
        ws.Cell(f, 4).Value = fleet.Summary.TotalBrokenDays;
        ws.Cell(f, 5).Value = "Eksik Gün";
        ws.Cell(f, 6).Value = fleet.Summary.MissingDays;

        ws.Columns().AdjustToContents();
    }

    // ── Sheet2 "İşlemler": muhasebeciye ham liste (tarih + CreatedAt sırasıyla gelir) ──────

    private static void RenderTransactions(IXLWorksheet ws, IReadOnlyList<MonthTxnRow> txns)
    {
        WriteHeader(ws, ["Tarih", "Tür", "Kategori", "Ödeme Yöntemi", "Tutar", "Not"]);

        var row = 2;
        foreach (var t in txns)
        {
            DateCell(ws.Cell(row, 1), t.Date);
            ws.Cell(row, 2).Value = TypeLabel(t.Type);
            ws.Cell(row, 3).Value = t.Category;
            ws.Cell(row, 4).Value = PaymentLabel(t.PaymentMethod);
            MoneyCell(ws.Cell(row, 5), t.AmountSatang);
            ws.Cell(row, 6).Value = t.Note ?? string.Empty;
            StripeRow(ws, row, 6);
            row++;
        }

        ws.Range(1, 1, row - 1, 6).SetAutoFilter();
        ws.Columns().AdjustToContents();
    }

    // ── Sheet3 "Kategori Dağılımı": önce Gelir bloğu, sonra Gider bloğu ────────────────────

    private static void RenderCategories(IXLWorksheet ws, MonthReportResponse r)
    {
        // "Tür" sütunu bloklara filtre kolaylığı verir; sıra: tüm Gelir satırları, sonra Gider.
        WriteHeader(ws, ["Tür", "Kategori", "Toplam"]);

        var row = 2;
        foreach (var (type, totals) in new[] { ("Gelir", r.IncomeByCategory), ("Gider", r.ExpenseByCategory) })
        {
            foreach (var t in totals)
            {
                ws.Cell(row, 1).Value = type;
                ws.Cell(row, 2).Value = t.Category;
                MoneyCell(ws.Cell(row, 3), t.TotalSatang);
                StripeRow(ws, row, 3);
                row++;
            }
        }

        ws.Range(1, 1, row - 1, 3).SetAutoFilter();
        ws.Columns().AdjustToContents();
    }

    // ── Ortak stil yardımcıları ────────────────────────────────────────────────────────────

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var range = ws.Range(1, 1, 1, headers.Length);
        range.Style.Fill.BackgroundColor = Navy;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
    }

    /// <summary>Tek çeviri noktası: satang → baht burada, server'da yapılır (I1).</summary>
    private static void MoneyCell(IXLCell cell, long satang)
    {
        cell.Value = satang / 100m;
        cell.Style.NumberFormat.Format = MoneyFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void DateCell(IXLCell cell, DateOnly date)
    {
        cell.Value = date.ToDateTime(TimeOnly.MinValue);
        cell.Style.DateFormat.Format = DateFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    /// <summary>Kiralama yüzdesi; snapshot yoksa (veya yüzde tanımsızsa) "—".</summary>
    private static void RentalCell(IXLCell cell, decimal? percent)
    {
        if (percent is null)
            cell.Value = "—";
        else
        {
            cell.Value = percent.Value;
            cell.Style.NumberFormat.Format = PercentFormat;
        }
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void LabelMoneyRow(IXLWorksheet ws, int row, string label, long satang, bool bold = false)
    {
        ws.Cell(row, 1).Value = label;
        MoneyCell(ws.Cell(row, 2), satang);
        if (bold)
            ws.Range(row, 1, row, 2).Style.Font.Bold = true;
    }

    private static void StripeRow(IXLWorksheet ws, int row, int lastColumn)
    {
        if ((row - 2) % 2 == 1)
            ws.Range(row, 1, row, lastColumn).Style.Fill.BackgroundColor = RowAlt;
    }

    /// <summary>"Amornrat Thanmaen (%90)" — isim ve yüzde DTO'dan (Settings kaynağı).</summary>
    private static string PartnerLabel(PartnerShareResponse p) =>
        $"{p.Name} (%{p.SharePercent.ToString("0.##", Tr)})";

    private static string TypeLabel(TransactionType type) =>
        type == TransactionType.Income ? "Gelir" : "Gider";

    /// <summary>labels.ts / DailyReportPdf ile aynı Türkçe ödeme yöntemi adları.</summary>
    private static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.CreditCard => "Kredi Kartı",
        PaymentMethod.BankTransfer => "Banka Transferi",
        _ => method.ToString()
    };
}
