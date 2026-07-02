using System.Globalization;
using Kasa.Api.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kasa.Api.Pdf;

/// <summary>
/// "KASA İŞLEM" günlük raporunun A4 dikey, TEK SAYFA PDF dizgisi (Bordro payslip kuralı).
/// Hesap yapılmaz (I1/I2): BuildDailyReportAsync'ten gelen hazır DailyReportResponse
/// yalnızca metne dökülür; tek bir toplama/yüzde bile burada üretilmez.
/// Kalabalık günlerde satır fontu kademeli küçülür (9pt taban), yine sığmazsa
/// ScaleToFit içerik ölçeğini düşürerek ikinci sayfaya taşmayı imkânsız kılar.
/// </summary>
public static class DailyReportPdf
{
    static DailyReportPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly Color Navy = Color.FromHex("#1F3864");
    private static readonly Color RowAlt = Color.FromHex("#F5F8FF");
    private static readonly Color Text = Color.FromHex("#24292F");
    private static readonly Color Muted = Color.FromHex("#6B7280");
    private static readonly Color Leader = Color.FromHex("#9AA6BF");
    private static readonly Color Danger = Color.FromHex("#C00000");

    public static byte[] Render(DailyReportResponse r)
    {
        // Kademeli font: gün kalabalıklaştıkça satır puntosu 10 → 9,5 → 9'a iner.
        var lineCount = r.IncomeLines.Count + r.ExpenseLines.Count;
        var rowFont = lineCount <= 24 ? 10f : lineCount <= 44 ? 9.5f : 9f;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            // Tahoma: Türkçe (İ/ğ/ş) ve ฿ kapsanır; ⚠ için Apple Symbols'a düşülür.
            page.DefaultTextStyle(t => t.FontFamily("Tahoma", "Apple Symbols").FontSize(rowFont).FontColor(Text));

            page.Content().ScaleToFit().Column(col =>
            {
                col.Spacing(7);

                col.Item().Background(Navy).PaddingVertical(9).PaddingHorizontal(12)
                    .Text($"KASA İŞLEM — {r.Date.ToString("d MMMM yyyy", Tr)}")
                    .FontSize(15).Bold().FontColor(Colors.White);

                col.Item().Text(text =>
                {
                    text.Span("Devir (önceki günden): ").FontColor(Muted);
                    text.Span(Baht(r.PreviousBalance)).Bold();
                });

                Section(col, "GELİR", c => Lines(c, r.IncomeLines, rowFont));
                Section(col, "GİDER", c => Lines(c, r.ExpenseLines, rowFont));

                col.Item().Row(row =>
                {
                    row.Spacing(14);
                    row.RelativeItem().Element(c => Distribution(c, "GELİR TÜRÜ DAĞILIMI", r.IncomeByCategory));
                    row.RelativeItem().Element(c => Distribution(c, "GİDER TÜRÜ DAĞILIMI", r.ExpenseByCategory));
                });

                Section(col, "GÜN NET", c => c.Column(net =>
                {
                    NetRow(net, "Gelirler Toplamı", r.IncomeTotal);
                    NetRow(net, "Giderler Toplamı", r.ExpenseTotal);
                    NetRow(net, $"POS Kesintisi (%{r.PosFeeRatePercent.ToString("0.##", Tr)})", r.PosFee);
                    NetRow(net, "Gün Net", r.DayNet);
                    NetRow(net, "+ Devir", r.PreviousBalance);

                    var negative = r.ClosingBalance < 0;
                    net.Item().BorderTop(1).BorderColor(Navy).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("ANA KASA").Bold()
                            .FontSize(rowFont + 1.5f).FontColor(negative ? Danger : Navy);
                        row.AutoItem().Text(Baht(r.ClosingBalance)).Bold()
                            .FontSize(rowFont + 1.5f).FontColor(negative ? Danger : Navy);
                    });
                }));

                col.Item().PaddingTop(2).Text(FleetLine(r))
                    .FontColor(r.FleetMissing ? Muted : Text)
                    .Italic(r.FleetMissing);
            });
        })).GeneratePdf();
    }

    /// <summary>Satang → "฿1,000.00" (eksi işaret ฿'nin önünde) — frontend format.ts ile aynı görünüm.</summary>
    private static string Baht(long satang)
    {
        var text = "฿" + Math.Abs(satang / 100m).ToString("N2", CultureInfo.InvariantCulture);
        return satang < 0 ? "-" + text : text;
    }

    /// <summary>labels.ts'teki Türkçe ödeme yöntemi adlarının birebir karşılığı.</summary>
    private static string PaymentLabel(Domain.PaymentMethod method) => method switch
    {
        Domain.PaymentMethod.Cash => "Nakit",
        Domain.PaymentMethod.CreditCard => "Kredi Kartı",
        Domain.PaymentMethod.BankTransfer => "Banka Transferi",
        _ => method.ToString()
    };

    private static void Section(ColumnDescriptor col, string title, Action<IContainer> body)
    {
        col.Item().Column(section =>
        {
            section.Item().Background(Navy).PaddingVertical(4).PaddingHorizontal(10)
                .Text(title).Bold().FontColor(Colors.White);
            section.Item().PaddingTop(4).PaddingHorizontal(2).Element(body);
        });
    }

    /// <summary>GELİR/GİDER kalemleri: "Kategori (Yöntem) ..... ฿tutar", not gri/italik alt satırda.</summary>
    private static void Lines(IContainer container, IReadOnlyList<ReportLineResponse> lines, float rowFont)
    {
        container.Column(col =>
        {
            if (lines.Count == 0)
            {
                col.Item().Text("— Kayıt yok —").Italic().FontColor(Muted);
                return;
            }

            foreach (var line in lines)
            {
                col.Item().PaddingVertical(1).Row(row =>
                {
                    row.AutoItem().Text($"{line.Category} ({PaymentLabel(line.PaymentMethod)})");
                    row.RelativeItem().PaddingHorizontal(4).AlignBottom().PaddingBottom(2.5f)
                        .LineHorizontal(0.8f).LineColor(Leader).LineDashPattern([0.8f, 2.2f]);
                    row.AutoItem().Text(Baht(line.AmountSatang));
                });

                if (!string.IsNullOrWhiteSpace(line.Note))
                    col.Item().PaddingLeft(10).Text(line.Note).Italic()
                        .FontColor(Muted).FontSize(rowFont - 1.5f);
            }
        });
    }

    /// <summary>Yan yana dağılım tabloları: alternating #F5F8FF satırlar, tutar sağa dayalı.</summary>
    private static void Distribution(IContainer container, string title, IReadOnlyList<CategoryTotalResponse> totals)
    {
        container.Column(col =>
        {
            col.Item().Background(Navy).PaddingVertical(4).PaddingHorizontal(10)
                .Text(title).Bold().FontColor(Colors.White);

            if (totals.Count == 0)
            {
                col.Item().PaddingTop(4).PaddingHorizontal(2)
                    .Text("— Kayıt yok —").Italic().FontColor(Muted);
                return;
            }

            for (var i = 0; i < totals.Count; i++)
            {
                col.Item().Background(i % 2 == 1 ? RowAlt : Colors.White)
                    .PaddingVertical(2.5f).PaddingHorizontal(6).Row(row =>
                    {
                        row.RelativeItem().Text(totals[i].Category);
                        row.AutoItem().Text(Baht(totals[i].TotalSatang));
                    });
            }
        });
    }

    private static void NetRow(ColumnDescriptor col, string label, long satang)
    {
        col.Item().PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().Text(label);
            row.AutoItem().Text(Baht(satang));
        });
    }

    private static string FleetLine(DailyReportResponse r)
    {
        if (r.Fleet is null)
            return "Filo verisi girilmedi";

        var f = r.Fleet;
        var parts = new List<string>
        {
            $"Toplam {f.TotalBikes}",
            f.BrokenAlert ? $"Arızalı {f.BrokenBikes} ⚠" : $"Arızalı {f.BrokenBikes}",
            $"Kirada {f.RentedBikes}"
        };
        if (f.RentalPercent is not null)
            parts.Add($"Kiralama %{f.RentalPercent.Value.ToString("0.0", CultureInfo.InvariantCulture)}");

        return "FİLO: " + string.Join("  |  ", parts);
    }
}
