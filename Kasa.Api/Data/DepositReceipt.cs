using Kasa.Domain;

namespace Kasa.Api.Data;

/// <summary>
/// Hasar depozitosu makbuzu — kasa'nın mali akışıyla SIFIR temas (Transaction/rapor/kasa
/// sayımı değişmez). Tek amaç: "DEP-YYYY-NNNNN" tekrarsız numara üretimi + yeniden yazdırma.
/// Status/iade/void/bakiye yok. Para satang'dır (I1) ama yalnız görüntüleme amaçlı.
/// </summary>
public class DepositReceipt
{
    public int Id { get; set; }

    /// <summary>"DEP-2026-00418". UNIQUE — server yıl bazlı üretir, tekrar edemez.</summary>
    public string No { get; set; } = "";

    /// <summary>Makbuz (düzenlenme) tarihi; numara yılı bundan türer.</summary>
    public DateOnly Date { get; set; }

    public string CustomerName { get; set; } = "";

    /// <summary>Opsiyonel müşteri telefonu; boşsa PDF'te satır görünmez.</summary>
    public string? Phone { get; set; }

    /// <summary>Opsiyonel MÜŞTERİ vergi/UID no'su (B2B/şirket kiralaması); boşsa gizli.</summary>
    public string? TaxId { get; set; }

    /// <summary>Marka dahil tam model metni ("Honda Click 160 Red"). Ayrı renk alanı yok.</summary>
    public string VehicleModel { get; set; } = "";

    /// <summary>Legacy (v3): yeni kayıtlarda model'e gömülüdür, boş kalır. Eski kayıtlar için korunur.</summary>
    public string? VehicleColor { get; set; }
    public string Plate { get; set; } = "";

    public long AmountSatang { get; set; }

    /// <summary>Mevcut ödeme enum'u aynen (Cash/CreditCard/BankTransfer).</summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Opsiyonel referans no; sabit "Ref No / เลขอ้างอิง" etiketiyle basılır (kartta TRACE NO,
    /// nakit/transferde fatura no). Etiket ödeme tipine göre değişmez. Boşsa gizli.
    /// </summary>
    public string? ReferenceNo { get; set; }

    public string FuelLevel { get; set; } = "Full";

    public DateTime HandoverAt { get; set; }
    public DateTime ReturnExpectedAt { get; set; }

    /// <summary>Legacy (v3) km limitleri — yeni usage kutusu DailyKm + RadiusPolicy'yi kullanır.</summary>
    public int LimitKmPerDay { get; set; } = 150;
    public int LimitRadiusKm { get; set; } = 150;

    /// <summary>Seçilen aracın günlük km limiti (usage kutusu metnini besler). POST'tan gelir.</summary>
    public int DailyKm { get; set; } = 150;

    /// <summary>Seçilen aracın yarıçap politikası: bangkok-only | within-150 | unlimited (usage metni).</summary>
    public string RadiusPolicy { get; set; } = "within-150";

    public DateTime CreatedAt { get; set; }
}
