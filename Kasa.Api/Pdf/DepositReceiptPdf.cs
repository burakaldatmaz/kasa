using System.Globalization;
using Kasa.Api.Data;
using Kasa.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kasa.Api.Pdf;

/// <summary>
/// Hasar depozitosu makbuzu — tek A4'te iki nüsha (üstte CUSTOMER, altta OFFICE), aralarında
/// kesikli FOLD &amp; CUT. Onaylı BKKBIKE tasarımı (navy #1F3864 / blue #2E75B6 / tint #F5F8FF,
/// Sarabun dizgi) birebir; v4 eklentileri: METHOD üç kutucuklu satır (seçili dolu + tik) ve üçüncü
/// bilgi kutusu "Late return". Hesap yok (I1): tutar hazır satang, IN WORDS Faz 12'den gelir.
/// İkonlar SVG ile çizilir (glif değil) — font zincirinden bağımsız. En dışta ScaleToFit tek sayfa
/// sigortasıdır (Bordro payslip kuralı: makbuz asla 2. sayfaya taşmaz).
/// </summary>
public static class DepositReceiptPdf
{
    static DepositReceiptPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        SarabunFonts.EnsureRegistered();
    }

    private const string Font = "Sarabun";

    // ── Düzenleyen kimliği (yasal ZORUNLU footer) ────────────────────────────────────────────
    // Gelir Vergisi K. m.105 ทวิ → düzenleyenin vergi kimlik no'su (TIN); DBD 2544 tebliği →
    // para tahsilat belgesinde işletme adresi. İkisi de makbuzda bulunmalı. Tek yerden değişsin
    // diye sabit; metinler birebir korunur (Thai unvan บริษัท…จำกัด, TIN 13 hane, tone: พระโขนงใต้).
    private const string CompanyNameTh = "บริษัท บีเอ็มเอ เทค โกลบอล จำกัด";
    private const string CompanyNameEn = "BMA Tech Global Co., Ltd.";
    private const string CompanyTaxLabelTh = "เลขประจำตัวผู้เสียภาษี";
    private const string CompanyTaxId = "0105567191722";
    private const string CompanyAddress = "5 ซอยสุขุมวิท 60/1 แขวงพระโขนงใต้ เขตพระโขนง กรุงเทพมหานคร 10260";
    private const string CompanyEmail = "reservations@bkkbike.com";

    // Uyarı kutuları (Refundable / Usage limits / Late return) metin puntosu — okunabilirlik için
    // v4 tasarımın 7 / 6.5pt'sinden büyütüldü. Tek sayfa ScaleToFit sigortası altında kalır.
    private const float NoticeFont = 8.5f; // EN lead+body ve Thai satırları
    private const float PillFont = 7.5f;   // sayısal "hap"lar (km/gün, ฿/km, saat kuralları)

    // ── BKKBIKE tasarım tokenları (v3/v4 ortak) ──────────────────────────────────────────────
    private static readonly Color Navy = Color.FromHex("#1F3864");
    private static readonly Color Blue = Color.FromHex("#2E75B6");
    private static readonly Color BlueSoft = Color.FromHex("#4A90D9");
    private static readonly Color Tint = Color.FromHex("#F5F8FF");
    private static readonly Color Line = Color.FromHex("#DCE3EC");
    private static readonly Color LineSoft = Color.FromHex("#E8EEF6");
    private static readonly Color Ink = Color.FromHex("#1A2332");
    private static readonly Color InkSoft = Color.FromHex("#475569");
    private static readonly Color Muted = Color.FromHex("#7B8794");
    private static readonly Color WarnBg = Color.FromHex("#FFFBEC");
    private static readonly Color WarnLine = Color.FromHex("#F0DFA6");
    private static readonly Color WarnInk = Color.FromHex("#7A5B12");
    private static readonly Color RefundBg = Color.FromHex("#F8FAFC");
    private static readonly Color DepositLbl = Color.FromHex("#C9D6EC");
    private static readonly Color DepositLblTh = Color.FromHex("#9FB4D6");
    private static readonly Color White = Colors.White;

    // Late return kutusu (PARÇA 3): yumuşak kırmızı tema — Refundable/Usage'dan ayrışır.
    private static readonly Color LateBg = Color.FromHex("#FDF3F0");
    private static readonly Color LateLine = Color.FromHex("#F0C8BC");
    private static readonly Color LateTitle = Color.FromHex("#7A2E1B");
    private static readonly Color LateBody = Color.FromHex("#8A3B25");
    private static readonly Color LateAccent = Color.FromHex("#C05B3E");

    // ── İkonlar (SVG path'leri tasarımdaki lucide setinden; renkler gömülü) ───────────────────
    private const string IconBike =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#FFFFFF' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'><circle cx='5.5' cy='17.5' r='3.5'/><circle cx='18.5' cy='17.5' r='3.5'/><path d='M15 17.5h-5l-3-6 3-2h4l2 4'/><path d='M9.5 9.5 12 6h3'/></svg>";

    private static string IconShield(string hex) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='{hex}' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'><path d='M12 2 4 5v6c0 5 3.4 8.5 8 10 4.6-1.5 8-5 8-10V5l-8-3z'/><path d='m9 12 2 2 4-4'/></svg>";

    private static string IconSpeed(string hex) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='{hex}' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'><path d='M3 12a9 9 0 1 1 18 0'/><path d='m13 13-3.5-2.5'/><circle cx='12' cy='13' r='1.4' fill='{hex}'/></svg>";

    private static string IconClock(string hex) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='{hex}' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/></svg>";

    private const string IconScissors =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#7B8794' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'><circle cx='6' cy='6' r='3'/><circle cx='6' cy='18' r='3'/><path d='M8.1 8.1 21 18M8.1 15.9 21 6'/></svg>";

    private const string IconCheck =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#FFFFFF' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'><path d='M20 6 9 17l-5-5'/></svg>";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static byte[] Render(DepositReceipt r)
    {
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(10, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(9).FontColor(Ink));

            // ScaleToFit: iki nüsha + fold-cut bir alt-piksel taşsa bile 2. sayfa açılmaz (tek sayfa sigortası).
            page.Content().ScaleToFit().Column(col =>
            {
                col.Item().Element(c => Copy(c, r, office: false));
                col.Item().Element(FoldCut);
                col.Item().Element(c => Copy(c, r, office: true));
            });
        })).GeneratePdf();
    }

    /// <summary>Tek nüsha (customer|office). İki kez çağrılır; tek fark rozet metni/rengidir.</summary>
    private static void Copy(IContainer container, DepositReceipt r, bool office)
    {
        container.Border(1).BorderColor(Line).CornerRadius(10).Column(copy =>
        {
            // Üst aksan şeridi: blue-soft → blue → navy (üç segmentle gradyan yaklaşımı).
            copy.Item().Height(5).Row(bar =>
            {
                bar.RelativeItem(38).Background(BlueSoft);
                bar.RelativeItem(62).Background(Blue);
                bar.RelativeItem(30).Background(Navy);
            });

            copy.Item().PaddingHorizontal(7, Unit.Millimetre).PaddingTop(6, Unit.Millimetre)
                .PaddingBottom(5, Unit.Millimetre).Column(inner =>
            {
                inner.Item().Element(c => Header(c, r, office));
                inner.Item().PaddingTop(5, Unit.Millimetre).Element(c => Body(c, r));
                inner.Item().PaddingTop(4, Unit.Millimetre).Element(c => Trio(c, r));
                inner.Item().PaddingTop(4, Unit.Millimetre).Element(c => Notices(c, r));
                inner.Item().PaddingTop(7, Unit.Millimetre).Element(Signatures);
                inner.Item().PaddingTop(5, Unit.Millimetre).Element(Footer);
            });
        });
    }

    private static void Header(IContainer container, DepositReceipt r, bool office)
    {
        container.BorderBottom(1).BorderColor(LineSoft).PaddingBottom(4, Unit.Millimetre).Row(row =>
        {
            // Marka: navy yuvarlak kare + bisiklet ikonu, yanında wordmark.
            row.RelativeItem().Row(brand =>
            {
                brand.AutoItem().Width(34).Height(34).CornerRadius(9).Background(Navy)
                    .Padding(6).Svg(IconBike);
                brand.AutoItem().PaddingLeft(9).AlignMiddle().Column(name =>
                {
                    name.Item().Text("BKKBIKE").FontSize(17).Bold().FontColor(Navy);
                    name.Item().PaddingTop(1).Text("MOTORBIKE RENTAL · BANGKOK")
                        .FontSize(6).SemiBold().LetterSpacing(0.12f).FontColor(Blue);
                });
            });

            row.RelativeItem().AlignRight().Column(right =>
            {
                var badge = office ? "OFFICE COPY · สำหรับร้าน" : "CUSTOMER COPY · สำหรับลูกค้า";
                right.Item().AlignRight().Background(office ? Blue : Navy)
                    .PaddingVertical(3).PaddingHorizontal(8).Text(badge)
                    .FontSize(6).Bold().LetterSpacing(0.08f).FontColor(White);

                right.Item().PaddingTop(5).AlignRight().Text("DAMAGE DEPOSIT RECEIPT")
                    .FontSize(12.5f).Bold().LetterSpacing(0.04f).FontColor(Navy);
                right.Item().AlignRight().Text("ใบรับเงินประกันความเสียหาย").FontSize(8).FontColor(InkSoft);
                right.Item().PaddingTop(4).AlignRight().Text(meta =>
                {
                    meta.DefaultTextStyle(t => t.FontSize(6.5f).FontColor(Muted));
                    meta.Span("No. ");
                    meta.Span(r.No).Bold().FontColor(Ink);
                    meta.Span("   ·   Date ");
                    meta.Span(r.Date.ToString("d MMM yyyy", Inv)).Bold().FontColor(Ink);
                });
            });
        });
    }

    private static void Body(IContainer container, DepositReceipt r)
    {
        container.Row(row =>
        {
            row.Spacing(7, Unit.Millimetre);

            // Sol: alıcı · (telefon) · (vergi no) · araç · plaka — opsiyonel satırlar boşsa atlanır.
            row.RelativeItem(38).Column(left =>
            {
                Field(left, "Received from / รับเงินจาก", r.CustomerName);

                if (!string.IsNullOrWhiteSpace(r.Phone))
                {
                    left.Item().PaddingTop(3, Unit.Millimetre);
                    Field(left, "Phone / โทรศัพท์", r.Phone);
                }

                if (!string.IsNullOrWhiteSpace(r.TaxId))
                {
                    left.Item().PaddingTop(3, Unit.Millimetre);
                    Field(left, "Tax ID / เลขประจำตัวผู้เสียภาษี", r.TaxId);
                }

                left.Item().PaddingTop(4, Unit.Millimetre);
                left.Item().Element(c => Label(c, "Vehicle / ยานพาหนะ"));
                left.Item().PaddingTop(1).Text(t =>
                {
                    t.Span(r.VehicleModel).FontSize(11).Bold().FontColor(Ink);
                    if (!string.IsNullOrWhiteSpace(r.VehicleColor))
                        t.Span($" · {r.VehicleColor}").FontSize(11).Medium().FontColor(InkSoft);
                });
                left.Item().PaddingTop(3).AlignLeft().Border(1.5f).BorderColor(Navy).CornerRadius(7)
                    .PaddingVertical(4).PaddingHorizontal(12)
                    .Text(r.Plate).FontSize(11.5f).Bold().LetterSpacing(0.1f).FontColor(Navy);
            });

            // Sağ: depozito kutusu · method · (ref no) · in words.
            row.RelativeItem(62).Column(right =>
            {
                right.Item().Element(c => DepositBox(c, r));
                right.Item().PaddingTop(3, Unit.Millimetre).Element(c => MethodBoxes(c, r.PaymentMethod));
                if (!string.IsNullOrWhiteSpace(r.ReferenceNo))
                    right.Item().PaddingTop(3, Unit.Millimetre).Element(c => RefNoLine(c, r.ReferenceNo));
                right.Item().PaddingTop(3, Unit.Millimetre).Element(c => InWords(c, r));
            });
        });
    }

    private static void DepositBox(IContainer container, DepositReceipt r)
    {
        container.Background(Navy).CornerRadius(9).PaddingVertical(5, Unit.Millimetre)
            .PaddingHorizontal(6, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Column(lbl =>
                {
                    lbl.Item().Text("DEPOSIT RECEIVED").FontSize(6.5f).Bold()
                        .LetterSpacing(0.08f).FontColor(DepositLbl);
                    lbl.Item().PaddingTop(1).Text("รับเงินประกันแล้ว").FontSize(7).FontColor(DepositLblTh);
                });
                row.AutoItem().AlignMiddle().Text(Baht(r.AmountSatang))
                    .FontSize(22).Bold().FontColor(White);
            });
    }

    /// <summary>v4: üç ödeme kutucuğu; seçili olan navy dolu + beyaz tik, diğerleri çerçeveli.</summary>
    private static void MethodBoxes(IContainer container, PaymentMethod selected)
    {
        (PaymentMethod Method, string Label)[] methods =
        [
            (PaymentMethod.Cash, "Cash"),
            (PaymentMethod.CreditCard, "Credit Card"),
            (PaymentMethod.BankTransfer, "Bank Transfer")
        ];

        container.Column(col =>
        {
            col.Item().Element(c => Label(c, "Method / วิธีชำระ"));
            col.Item().PaddingTop(2).Row(row =>
            {
                row.Spacing(4);
                foreach (var (method, label) in methods)
                {
                    var on = method == selected;
                    row.RelativeItem().Background(on ? Navy : White).Border(1)
                        .BorderColor(on ? Navy : Line).CornerRadius(6)
                        .PaddingVertical(4).PaddingHorizontal(6).Row(box =>
                        {
                            box.AutoItem().Width(9).Height(9).AlignMiddle().Element(mark =>
                            {
                                if (on)
                                    mark.Svg(IconCheck);
                                else
                                    mark.Height(9).Width(9).CornerRadius(4.5f).Border(1).BorderColor(Line);
                            });
                            box.RelativeItem().PaddingLeft(4).AlignMiddle().Text(label)
                                .FontSize(8).SemiBold().FontColor(on ? White : Muted);
                        });
                }
            });
        });
    }

    private static void InWords(IContainer container, DepositReceipt r)
    {
        container.Background(Tint).Border(1).BorderColor(LineSoft).CornerRadius(8)
            .PaddingVertical(3, Unit.Millimetre).PaddingHorizontal(4, Unit.Millimetre).Column(col =>
            {
                Label(col.Item(), "In words / ตัวอักษร");
                col.Item().PaddingTop(2).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(9).Bold().FontColor(Ink));
                    t.Span(BahtText.ToEnglishWords(r.AmountSatang));
                    t.Span(" · ").FontColor(Muted);
                    t.Span(BahtText.ToThaiWords(r.AmountSatang));
                });
            });
    }

    /// <summary>Ref No satırı (PARÇA 1/4): sabit iki dilli etiket + değer, tek kompakt satır.</summary>
    private static void RefNoLine(IContainer container, string reference)
    {
        container.Background(Tint).Border(1).BorderColor(LineSoft).CornerRadius(8)
            .PaddingVertical(2.5f, Unit.Millimetre).PaddingHorizontal(4, Unit.Millimetre).Row(row =>
            {
                row.AutoItem().AlignMiddle().Element(c => Label(c, "Ref No / เลขอ้างอิง"));
                row.RelativeItem().PaddingLeft(8).AlignMiddle().AlignRight()
                    .Text(reference).FontSize(9).Bold().FontColor(Ink);
            });
    }

    private static void Trio(IContainer container, DepositReceipt r)
    {
        container.Row(row =>
        {
            row.Spacing(5, Unit.Millimetre);
            TrioCell(row, "Handover", r.HandoverAt.ToString("d MMM yyyy · HH:mm", Inv));
            TrioCell(row, "Fuel", string.IsNullOrWhiteSpace(r.FuelLevel) ? "—" : r.FuelLevel);
            TrioCell(row, "Return (exp.)", r.ReturnExpectedAt.ToString("d MMM yyyy · HH:mm", Inv));
        });
    }

    private static void TrioCell(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor(Line).CornerRadius(8)
            .PaddingVertical(3.5f, Unit.Millimetre).PaddingHorizontal(4, Unit.Millimetre).Column(cell =>
            {
                cell.Item().Text(label.ToUpperInvariant()).FontSize(5.5f).Bold()
                    .LetterSpacing(0.1f).FontColor(Muted);
                cell.Item().PaddingTop(2).Text(value).FontSize(10).Bold().FontColor(Ink);
            });
    }

    private static void Notices(IContainer container, DepositReceipt r)
    {
        container.Column(col =>
        {
            col.Item().Element(c => Notice(c, RefundBg, Line, InkSoft, Ink, IconShield("#2F7D32"),
                "Refundable.",
                " This deposit is returned in full when the motorbike is returned in its original condition. Deductions may apply for damage, missing parts/accessories, traffic fines, fuel, or late return.",
                "เงินประกันนี้คืนเต็มจำนวนเมื่อคืนรถในสภาพเดิม อาจหักกรณีรถเสียหาย อุปกรณ์สูญหาย ค่าปรับจราจร ค่าน้ำมัน หรือคืนรถล่าช้า"));

            col.Item().PaddingTop(3, Unit.Millimetre).Element(c => UsageNotice(c, r));
            col.Item().PaddingTop(3, Unit.Millimetre).Element(LateReturnNotice);
        });
    }

    /// <summary>
    /// Usage limits kutusu (PARÇA 2): metin aracın DailyKm + RadiusPolicy'sine göre değişir.
    /// Sayısal ibareler pill'li; aşım ücreti (2 ฿/km) cümlenin sonunda, tek kutuda kalır.
    /// </summary>
    private static void UsageNotice(IContainer container, DepositReceipt r)
    {
        NoticeShell(container, WarnBg, WarnLine, null, IconSpeed("#7A5B12"), col =>
        {
            col.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(NoticeFont).LineHeight(1.6f).FontColor(WarnInk));
                t.Span("Usage limits.").Bold().FontColor(WarnInk);
                t.Span(" Max ");
                Pill(t, $"{r.DailyKm} km/day", WarnInk, WarnLine);
                t.Span(" · ");
                switch (r.RadiusPolicy)
                {
                    case "bangkok-only":
                        t.Span("not allowed to go out of Bangkok").Bold();
                        break;
                    case "unlimited":
                        t.Span("no distance limit").Bold();
                        break;
                    default: // within-150
                        t.Span("travel ");
                        Pill(t, "within 150 km", WarnInk, WarnLine);
                        t.Span(" of Bangkok");
                        break;
                }
                t.Span(". Excess ");
                Pill(t, "2 ฿/km", WarnInk, WarnLine);
                t.Span(".");
            });
            col.Item().PaddingTop(1).Text(ThaiUsage(r)).FontSize(NoticeFont).LineHeight(1.4f).FontColor(InkSoft);
        });
    }

    private static string ThaiUsage(DepositReceipt r) => r.RadiusPolicy switch
    {
        "bangkok-only" => $"ไม่เกิน {r.DailyKm} กม./วัน · ไม่อนุญาตให้ออกนอกกรุงเทพฯ · ส่วนเกิน 2 บาท/กม.",
        "unlimited" => $"ไม่เกิน {r.DailyKm} กม./วัน · ไม่จำกัดระยะทาง · ส่วนเกิน 2 บาท/กม.",
        _ => $"ไม่เกิน {r.DailyKm} กม./วัน · อยู่ในรัศมี 150 กม. จากกรุงเทพฯ · ส่วนเกิน 2 บาท/กม.",
    };

    /// <summary>
    /// Late return kutusu (PARÇA 3): onaylı gecikme kuralı. Yumuşak kırmızı tema + sol aksan +
    /// saat ikonu. Gecikme kuralları ("2–4 hrs · 50% of daily rate" ve "over 4 hrs · full day
    /// charge") burada BİLEREK pill değil, inline kalın/renkli span'dir: QuestPDF inline
    /// <see cref="Pill"/> (text.Element) kutularını span akışından SONRA çizer, bu yüzden
    /// pdftotext/PdfPig gibi metin çıkarıcılar (ve ekran okuyucular) onları cümlenin sonuna kaydırır
    /// ("First hour free · · . …" + kopuk pill'ler). Span kullanınca çizim sırası = okuma sırası
    /// kalır; cümle soldan sağa kesintisiz çıkar. Görsel vurgu SemiBold + koyu kırmızıyla korunur.
    /// </summary>
    private static void LateReturnNotice(IContainer container)
    {
        NoticeShell(container, LateBg, LateLine, LateAccent, IconClock("#C05B3E"), col =>
        {
            col.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(NoticeFont).LineHeight(1.6f).FontColor(LateBody));
                t.Span("Late return.").Bold().FontColor(LateTitle);
                t.Span(" First hour free · ");
                t.Span("2–4 hrs · 50% of daily rate").SemiBold().FontColor(LateTitle);
                t.Span(" · ");
                t.Span("over 4 hrs · full day charge").SemiBold().FontColor(LateTitle);
                t.Span(". Returns after closing time (19:00) are always charged as a full extra day.");
            });
            col.Item().PaddingTop(1).Text(
                    "คืนรถล่าช้า — ชั่วโมงแรกไม่คิดค่าใช้จ่าย · ชั่วโมงที่ 2–4 คิด 50% ของค่าเช่ารายวัน · เกิน 4 ชม. คิดค่าเช่าเต็มวัน · การคืนรถหลังเวลาปิดร้าน 19:00 น. คิดค่าเช่าเต็มวันทุกกรณี")
                .FontSize(NoticeFont).LineHeight(1.4f).FontColor(LateBody);
        });
    }

    /// <summary>Sabit metinli basit uyarı (Refundable). Pill'siz; iki dilli lead + gövde.</summary>
    private static void Notice(
        IContainer container, Color bg, Color border, Color textColor, Color leadColor, string icon,
        string lead, string body, string thai)
    {
        NoticeShell(container, bg, border, null, icon, col =>
        {
            col.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(NoticeFont).LineHeight(1.45f).FontColor(textColor));
                t.Span(lead).Bold().FontColor(leadColor);
                t.Span(body);
            });
            col.Item().PaddingTop(1).Text(thai).FontSize(NoticeFont).LineHeight(1.4f).FontColor(InkSoft);
        });
    }

    /// <summary>
    /// Uyarı kutusu iskeleti: opsiyonel sol renk aksanı (late return için) + ikon + içerik kolonu.
    /// PaddingTop boyuttan ÖNCE: ikon kutusu 14×14 kare kalır, padding kutuyu satır içinde 1px iter.
    /// </summary>
    private static void NoticeShell(
        IContainer container, Color bg, Color border, Color? leftAccent, string icon,
        Action<ColumnDescriptor> content)
    {
        void Frame(IContainer c) =>
            c.Background(bg).Border(1).BorderColor(border).CornerRadius(8)
                .PaddingVertical(3.5f, Unit.Millimetre).PaddingHorizontal(4, Unit.Millimetre).Row(row =>
                {
                    row.AutoItem().PaddingTop(1).Width(14).Height(14).Svg(icon);
                    row.RelativeItem().PaddingLeft(9).Column(content);
                });

        if (leftAccent is { } accent)
            container.Background(accent).CornerRadius(8).PaddingLeft(2.5f).Element(Frame);
        else
            Frame(container);
    }

    /// <summary>
    /// Sayısal ibareyi çerçeveleyen inline "hap" (pill) — usage ve late-return kutularında ORTAK
    /// bileşen (PARÇA 2/3). Beyaz zemin + ince renkli çerçeve + kalın mürekkep; metin ortasına hizalı.
    /// </summary>
    private static void Pill(TextDescriptor text, string value, Color ink, Color border)
    {
        text.Element()
            .PaddingBottom(1)
            .Background(White).Border(0.8f).BorderColor(border).CornerRadius(3)
            .PaddingHorizontal(3).PaddingVertical(0.5f)
            .Text(value).FontSize(PillFont).SemiBold().FontColor(ink).LineHeight(1f);
    }

    private static void Signatures(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(14, Unit.Millimetre);
            SignCell(row, "Received by (BKKBIKE) / ผู้รับเงิน");
            SignCell(row, "Customer / ลายเซ็นลูกค้า");
        });
    }

    private static void SignCell(RowDescriptor row, string caption)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().PaddingTop(6, Unit.Millimetre).BorderTop(1.4f).BorderColor(Ink);
            col.Item().PaddingTop(3).AlignCenter().Text(caption).FontSize(7).FontColor(InkSoft);
        });
    }

    /// <summary>
    /// Alt bilgi: solda saklama uyarısı; sağda düzenleyen kimliği iki satır (yasal zorunlu).
    /// Satır 1 = unvan (TH·EN bold) + TIN (normal); satır 2 = işletme adresi + e-posta. Puntoyu
    /// footer stiline göre 0.2pt kısıp (6.3) TIN satırını tek satırda tutar; kutulara dokunmaz.
    /// </summary>
    private static void Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(LineSoft).PaddingTop(4, Unit.Millimetre).Row(row =>
        {
            row.RelativeItem().AlignMiddle()
                .Text("Keep this receipt to reclaim your deposit · เก็บใบนี้ไว้เพื่อขอรับเงินประกันคืน")
                .FontSize(6.5f).FontColor(Muted);

            row.AutoItem().PaddingLeft(6).AlignMiddle().Column(org =>
            {
                org.Item().AlignRight().Text(line =>
                {
                    line.DefaultTextStyle(s => s.FontSize(6.3f).FontColor(Muted));
                    line.Span(CompanyNameTh).Bold().FontColor(Ink);
                    line.Span(" · ");
                    line.Span(CompanyNameEn).Bold().FontColor(Ink);
                    line.Span($" · {CompanyTaxLabelTh} ");
                    line.Span(CompanyTaxId); // TIN: normal ağırlık
                });
                org.Item().PaddingTop(1).AlignRight()
                    .Text($"{CompanyAddress} · {CompanyEmail}")
                    .FontSize(6.3f).FontColor(Muted);
            });
        });
    }

    private static void FoldCut(IContainer container)
    {
        container.PaddingVertical(3.5f, Unit.Millimetre).Row(row =>
        {
            row.RelativeItem().AlignMiddle().LineHorizontal(1.2f).LineColor(Line);
            row.AutoItem().PaddingHorizontal(10).AlignMiddle().Row(mid =>
            {
                mid.AutoItem().Width(11).Height(11).AlignMiddle().Svg(IconScissors);
                mid.AutoItem().PaddingLeft(6).AlignMiddle().Text("FOLD & CUT · พับแล้วตัด")
                    .FontSize(6.5f).Bold().LetterSpacing(0.15f).FontColor(Muted);
            });
            row.RelativeItem().AlignMiddle().LineHorizontal(1.2f).LineColor(Line);
        });
    }

    // ── Küçük yardımcılar ────────────────────────────────────────────────────────────────────
    private static void Field(ColumnDescriptor col, string label, string value)
    {
        Label(col.Item(), label);
        col.Item().PaddingTop(1).Text(value).FontSize(11).Bold().FontColor(Ink);
    }

    private static void Label(IContainer container, string text) =>
        container.Text(text).FontSize(6).Bold().LetterSpacing(0.1f).FontColor(Blue);

    /// <summary>Satang → "฿3,000.00" (Sarabun ฿ glifini içerir). Depozito daima pozitiftir.</summary>
    private static string Baht(long satang) =>
        "฿" + (satang / 100m).ToString("N2", Inv);
}
