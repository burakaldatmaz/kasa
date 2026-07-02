using System.Globalization;
using Kasa.Api.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kasa.Api.Pdf;

/// <summary>
/// "KASA İŞLEM" günlük raporunun A4 dikey, TEK SAYFA PDF dizgisi — v2 tasarım (Faz 9).
/// Hesap yapılmaz (I1/I2): BuildDailyReportAsync'ten gelen hazır DailyReportResponse
/// yalnızca dizgiye dökülür; tek bir toplama/yüzde bile burada üretilmez.
/// GELİR/GİDER blokları yan yana dizilir (tek sayfa alanı ikiye bölünür), kalabalık
/// günlerde satır fontu kademeli küçülür (10 → 9 → 8.5), yine sığmazsa ScaleToFit
/// içerik ölçeğini düşürerek ikinci sayfaya taşmayı imkânsız kılar.
/// İkon glifi kullanılmaz (container font zincirinde yok — Faz 8 ⚠ dersi):
/// tür işaretleri ve filo noktası fontsuz çizilen küçük renkli dairelerdir.
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
    private static readonly Color Border = Color.FromHex("#C9D2E4");
    private static readonly Color Danger = Color.FromHex("#C00000");
    private static readonly Color SoftWhite = Color.FromHex("#C7D2E8");
    private static readonly Color IncomeBg = Color.FromHex("#E2EFDA");
    private static readonly Color IncomeInk = Color.FromHex("#375623");
    private static readonly Color ExpenseBg = Color.FromHex("#FBE2E2");
    private static readonly Color ExpenseInk = Color.FromHex("#9C1C1C");
    private static readonly Color NeutralBg = Color.FromHex("#EDF1F8");
    private static readonly Color StripBg = Color.FromHex("#F2F4F7");
    private static readonly Color Orange = Color.FromHex("#C55A11");

    /// <param name="generatedAt">"Oluşturma" damgası — endpoint Bangkok saatini (UTC+7) verir.</param>
    public static byte[] Render(DailyReportResponse r, DateTimeOffset generatedAt)
    {
        // Kademeli font: gün kalabalıklaştıkça satır puntosu 10 → 9 → 8.5'e iner.
        // Bloklar yan yana olduğundan eşikler eski dikey düzenden daha geniştir.
        var lineCount = r.IncomeLines.Count + r.ExpenseLines.Count;
        var rowFont = lineCount <= 30 ? 10f : lineCount <= 52 ? 9f : 8.5f;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            // Tahoma: Türkçe (İ/ğ/ş) ve ฿ kapsanır; kapsam dışı glif için Apple Symbols'a düşülür.
            page.DefaultTextStyle(t => t.FontFamily("Tahoma", "Apple Symbols").FontSize(rowFont).FontColor(Text));

            page.Content().ScaleToFit().Column(col =>
            {
                col.Spacing(8);

                col.Item().Element(c => Header(c, r, generatedAt));
                col.Item().Element(c => SummaryCards(c, r));

                // Kalem blokları yan yana: kalem sayıları asimetrikse bloklar bağımsız uzar.
                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(c => LinesBlock(c, "GELİR", r.IncomeLines, r.IncomeTotal));
                    row.RelativeItem().Element(c => LinesBlock(c, "GİDER", r.ExpenseLines, r.ExpenseTotal));
                });

                col.Item().Text("(N) Nakit    (KK) Kredi Kartı    (BT) Banka Transferi")
                    .FontSize(7).FontColor(Muted);

                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(c => Distribution(c, r));
                    row.RelativeItem().Element(c => DayAccount(c, r, rowFont));
                });

                col.Item().Element(c => FleetStrip(c, r));
            });

            page.Footer().PaddingTop(6).Column(footer =>
            {
                footer.Item().LineHorizontal(0.8f).LineColor(Border);
                footer.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem().Text("BKKBIKE — BMA Tech Global Co., Ltd.")
                        .FontSize(7).FontColor(Muted);
                    row.AutoItem().Text("kasa.bkkbike.com").FontSize(7).FontColor(Muted);
                });
            });
        })).GeneratePdf();
    }

    /// <summary>Üst bant: solda marka, sağda "KASA İŞLEM — {gün adıyla tarih}" + oluşturma damgası.</summary>
    private static void Header(IContainer container, DailyReportResponse r, DateTimeOffset generatedAt)
    {
        container.Background(Navy).PaddingVertical(10).PaddingHorizontal(12).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("BKKBIKE").FontSize(20).Bold().FontColor(Colors.White);
                left.Item().Text("Günlük Kasa Raporu").FontSize(9).FontColor(SoftWhite);
            });

            row.RelativeItem().AlignRight().AlignMiddle().Column(right =>
            {
                right.Item().AlignRight().Text(text =>
                {
                    text.Span("KASA İŞLEM — ").FontSize(8).FontColor(SoftWhite);
                    text.Span(r.Date.ToString("d MMMM yyyy, dddd", Tr))
                        .FontSize(14).Bold().FontColor(Colors.White);
                });
                right.Item().AlignRight()
                    .Text($"Oluşturma: {generatedAt.ToString("dd.MM.yyyy HH:mm", Tr)}")
                    .FontSize(7.5f).FontColor(SoftWhite);
            });
        });
    }

    /// <summary>4 özet kartı: rapora bakan kişi 2 saniyede günü görür. Değerler DTO'dan hazır gelir.</summary>
    private static void SummaryCards(IContainer container, DailyReportResponse r)
    {
        var netNegative = r.DayNet < 0;
        var closingNegative = r.ClosingBalance < 0;

        container.Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem().Element(c => Card(c, "GELİR", r.IncomeTotal, IncomeBg, IncomeInk));
            row.RelativeItem().Element(c => Card(c, "GİDER", r.ExpenseTotal, ExpenseBg, ExpenseInk));
            row.RelativeItem().Element(c => Card(c, "GÜN NET", r.DayNet,
                netNegative ? ExpenseBg : NeutralBg, netNegative ? Danger : Navy));
            // ANA KASA en vurgulu kart: dolu zemin; negatifse zemin kırmızıya döner.
            row.RelativeItem().Element(c => Card(c, "ANA KASA", r.ClosingBalance,
                closingNegative ? Danger : Navy, Colors.White));
        });
    }

    private static void Card(IContainer container, string label, long satang, Color bg, Color ink)
    {
        container.CornerRadius(3).Background(bg).PaddingVertical(7).PaddingHorizontal(9).Column(card =>
        {
            card.Spacing(2);
            card.Item().Text(label).FontSize(7.5f).Bold().FontColor(ink);
            card.Item().Text(Baht(satang)).FontSize(13).Bold().FontColor(ink);
        });
    }

    /// <summary>Satang → "฿1,000.00" (eksi işaret ฿'nin önünde) — frontend format.ts ile aynı görünüm.</summary>
    private static string Baht(long satang)
    {
        var text = "฿" + Math.Abs(satang / 100m).ToString("N2", CultureInfo.InvariantCulture);
        return satang < 0 ? "-" + text : text;
    }

    /// <summary>Uzun kategori adları satır taşırmasın diye lejantlı kısaltmalar.</summary>
    private static string PaymentAbbr(Domain.PaymentMethod method) => method switch
    {
        Domain.PaymentMethod.Cash => "N",
        Domain.PaymentMethod.CreditCard => "KK",
        Domain.PaymentMethod.BankTransfer => "BT",
        _ => method.ToString()
    };

    /// <summary>
    /// GELİR/GİDER bloğu: başlık şeridi + alternating satırlar ("Kategori (N) | tutar",
    /// not gri italik alt satır) + koyu ince çizgili "Toplam" (tutar DTO'dan, toplanmaz — I1).
    /// </summary>
    private static void LinesBlock(
        IContainer container, string title, IReadOnlyList<ReportLineResponse> lines, long totalSatang)
    {
        container.Column(col =>
        {
            col.Item().Background(Navy).PaddingVertical(4).PaddingHorizontal(8).Row(head =>
            {
                head.RelativeItem().Text(title).Bold().FontColor(Colors.White);
                head.AutoItem().AlignBottom().Text($"{lines.Count} kalem").FontSize(7.5f).FontColor(SoftWhite);
            });

            if (lines.Count == 0)
            {
                col.Item().PaddingVertical(5).PaddingHorizontal(8)
                    .Text("— Kayıt yok —").Italic().FontColor(Muted);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                col.Item().Background(i % 2 == 1 ? RowAlt : Colors.White)
                    .PaddingVertical(1.5f).PaddingHorizontal(6).Column(item =>
                    {
                        item.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{line.Category} ({PaymentAbbr(line.PaymentMethod)})");
                            row.AutoItem().PaddingLeft(6).Text(Baht(line.AmountSatang));
                        });

                        if (!string.IsNullOrWhiteSpace(line.Note))
                            item.Item().PaddingLeft(6).Text(line.Note).Italic()
                                .FontColor(Muted).FontSize(7.5f);
                    });
            }

            col.Item().BorderTop(1).BorderColor(Navy)
                .PaddingVertical(2.5f).PaddingHorizontal(6).Row(row =>
                {
                    row.RelativeItem().Text("Toplam").Bold();
                    row.AutoItem().Text(Baht(totalSatang)).Bold();
                });
        });
    }

    /// <summary>
    /// Tür Dağılımı: gelir + gider kategorileri tek kompakt tabloda (Faz 7 Sheet3 deseni:
    /// önce Gelir satırları, sonra Gider). Tür işareti fontsuz çizilen renkli dairedir.
    /// </summary>
    private static void Distribution(IContainer container, DailyReportResponse r)
    {
        container.Column(col =>
        {
            col.Item().Background(Navy).PaddingVertical(4).PaddingHorizontal(8)
                .Text("TÜR DAĞILIMI").Bold().FontColor(Colors.White);

            var rows = r.IncomeByCategory.Select(t => (Total: t, Income: true))
                .Concat(r.ExpenseByCategory.Select(t => (Total: t, Income: false)))
                .ToList();

            if (rows.Count == 0)
            {
                col.Item().PaddingVertical(5).PaddingHorizontal(8)
                    .Text("— Kayıt yok —").Italic().FontColor(Muted);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var (total, income) = rows[i];
                col.Item().Background(i % 2 == 1 ? RowAlt : Colors.White)
                    .PaddingVertical(2f).PaddingHorizontal(6).Row(row =>
                    {
                        row.ConstantItem(10).AlignMiddle().Element(dot =>
                            dot.Width(5).Height(5).CornerRadius(2.5f)
                                .Background(income ? IncomeInk : Danger));
                        row.RelativeItem().PaddingLeft(2).Text(total.Category);
                        row.AutoItem().Text(Baht(total.TotalSatang));
                    });
            }
        });
    }

    /// <summary>
    /// Gün Hesabı kutusu: toplamlardan ANA KASA'ya inen akış. Tüm değerler DTO'dan
    /// hazır gelir; çift çizgi altındaki ANA KASA negatifse kırmızıdır.
    /// </summary>
    private static void DayAccount(IContainer container, DailyReportResponse r, float rowFont)
    {
        container.CornerRadius(3).Border(0.8f).BorderColor(Border)
            .PaddingVertical(6).PaddingHorizontal(8).Column(box =>
            {
                box.Item().Text("GÜN HESABI").Bold().FontColor(Navy).FontSize(8);

                box.Item().PaddingTop(2).Column(net =>
                {
                    NetRow(net, "Gelirler Toplamı", r.IncomeTotal);
                    NetRow(net, "Giderler Toplamı", r.ExpenseTotal);
                    NetRow(net, $"POS Kesintisi (%{r.PosFeeRatePercent.ToString("0.##", Tr)})", r.PosFee);
                    NetRow(net, "Gün Net", r.DayNet);
                    NetRow(net, "+ Devir (önceki günden):", r.PreviousBalance);

                    // Muhasebe kapanışı: çift çizgi + büyük punto ANA KASA.
                    net.Item().PaddingTop(3).LineHorizontal(1).LineColor(Navy);
                    net.Item().PaddingTop(1).LineHorizontal(1).LineColor(Navy);

                    var negative = r.ClosingBalance < 0;
                    net.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("ANA KASA").Bold()
                            .FontSize(rowFont + 2f).FontColor(negative ? Danger : Navy);
                        row.AutoItem().Text(Baht(r.ClosingBalance)).Bold()
                            .FontSize(rowFont + 2f).FontColor(negative ? Danger : Navy);
                    });
                });
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

    /// <summary>Filo şeridi: tam genişlik açık gri zemin; arızalı sayısı >0 ise turuncu vurgu.</summary>
    private static void FleetStrip(IContainer container, DailyReportResponse r)
    {
        container.CornerRadius(3).Background(StripBg)
            .PaddingVertical(6).PaddingHorizontal(10).Element(strip =>
            {
                if (r.Fleet is null)
                {
                    strip.Text("Filo verisi girilmedi").Italic().FontColor(Muted);
                    return;
                }

                var f = r.Fleet;
                strip.Row(row =>
                {
                    row.ConstantItem(12).AlignMiddle().Element(dot =>
                        dot.Width(7).Height(7).CornerRadius(3.5f).Background(Navy));

                    row.RelativeItem().Text(text =>
                    {
                        text.Span("FİLO:").Bold().FontColor(Navy);
                        text.Span($"  Toplam {f.TotalBikes}");
                        Divider(text);

                        var broken = $"Arızalı {f.BrokenBikes}";
                        if (f.BrokenAlert)
                            text.Span(broken).Bold().FontColor(Orange);
                        else
                            text.Span(broken);

                        Divider(text);
                        text.Span($"Kirada {f.RentedBikes}");
                        Divider(text);
                        text.Span($"Boşta {f.IdleBikes}");

                        if (f.RentalPercent is not null)
                        {
                            Divider(text);
                            text.Span($"Kiralama %{f.RentalPercent.Value.ToString("0.0", CultureInfo.InvariantCulture)}");
                        }

                        // Rezervasyon sayaçları (Faz 11): null = "girilmedi" → "—" (K2).
                        Divider(text);
                        text.Span($"Başlayan {Count(f.StartedReservations)}");
                        Divider(text);
                        text.Span($"Biten {Count(f.EndedReservations)}");
                    });
                });
            });

        static void Divider(TextDescriptor text) => text.Span("   |   ").FontColor(Muted);

        static string Count(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "—";
    }
}
