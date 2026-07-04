using System.Globalization;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Kasa.Api.Pdf;
using Kasa.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Endpoints;

/// <summary>
/// /api/deposit-receipts — bağımsız depozito makbuzu modülü. Kasa'nın mali akışıyla temassız:
/// yalnız kayıt + numara üretimi + PDF. Düzenleme/silme/iade yok (yanlış girildiyse yenisi kesilir).
/// </summary>
public static class DepositReceiptEndpoints
{
    private const int MaxNumberAttempts = 6;

    // Tek instance (tek kullanıcı) uygulamada numara üretimi süreç genelinde serialize edilir:
    // "oku MAX → ata → kaydet" kritik bölümü hiç yarışmaz, dolayısıyla iki makbuz aynı numarayı
    // alamaz. UNIQUE index yine son sigortadır (buraya rağmen bir çakışma olursa yeniden denenir).
    private static readonly SemaphoreSlim NumberGate = new(1, 1);

    public static void MapDepositReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deposit-receipts");

        // Gün listesi (yeniden yazdırma): No'ya göre artan — yılın sırası sabit genişlikte olduğundan
        // sözlük sırası = sayısal sıra.
        group.MapGet("/", async (KasaDbContext db, DateOnly? date) =>
        {
            if (date is null)
                return Results.BadRequest(new ErrorResponse("date parametresi zorunludur (örn: ?date=2026-07-03)."));

            var receipts = await db.DepositReceipts.AsNoTracking()
                .Where(r => r.Date == date)
                .OrderBy(r => r.No)
                .Select(r => ToResponse(r))
                .ToListAsync();

            return Results.Ok(receipts);
        });

        group.MapPost("/", async (KasaDbContext db, TimeProvider clock, SaveDepositReceiptRequest request) =>
        {
            var validation = Validate(request);
            if (validation.Error is not null)
                return validation.Error;

            var year = request.Date.Year;
            DepositReceipt receipt;

            await NumberGate.WaitAsync();
            try
            {
                var attempt = 0;
                while (true)
                {
                    receipt = new DepositReceipt
                    {
                        No = await NextNumberAsync(db, year),
                        Date = request.Date,
                        CustomerName = validation.CustomerName!,
                        Phone = validation.Phone,
                        TaxId = validation.TaxId,
                        VehicleModel = validation.VehicleModel!,
                        VehicleColor = validation.VehicleColor,
                        Plate = validation.Plate!,
                        AmountSatang = validation.AmountSatang,
                        PaymentMethod = request.PaymentMethod,
                        ReferenceNo = validation.ReferenceNo,
                        FuelLevel = validation.FuelLevel!,
                        HandoverAt = request.HandoverAt,
                        ReturnExpectedAt = request.ReturnExpectedAt,
                        LimitKmPerDay = validation.LimitKmPerDay,
                        LimitRadiusKm = validation.LimitRadiusKm,
                        DailyKm = validation.DailyKm,
                        RadiusPolicy = validation.RadiusPolicy!,
                        CreatedAt = clock.GetUtcNow().UtcDateTime
                    };
                    db.DepositReceipts.Add(receipt);

                    try
                    {
                        await db.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateException) when (++attempt < MaxNumberAttempts)
                    {
                        // UNIQUE çakışması (beklenmez ama sigorta): numarayı geri al, yeniden dene.
                        db.Entry(receipt).State = EntityState.Detached;
                    }
                }
            }
            finally
            {
                NumberGate.Release();
            }

            return Results.Created($"/api/deposit-receipts/{receipt.Id}", ToResponse(receipt));
        });

        // Makbuz PDF'i: dosya adı numaranın kendisi ("DEP-2026-00418.pdf").
        group.MapGet("/{id:int}/pdf", async (KasaDbContext db, int id) =>
        {
            var receipt = await db.DepositReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (receipt is null)
                return Results.NotFound(new ErrorResponse("Makbuz bulunamadı."));

            return Results.File(DepositReceiptPdf.Render(receipt), "application/pdf", $"{receipt.No}.pdf");
        });
    }

    /// <summary>Yıl içindeki bir sonraki sıra numarası: en yüksek No'yu okur, sırasını 1 artırır.</summary>
    private static async Task<string> NextNumberAsync(KasaDbContext db, int year)
    {
        var prefix = $"DEP-{year:0000}-";
        var lastNo = await db.DepositReceipts
            .Where(r => r.No.StartsWith(prefix))
            .OrderByDescending(r => r.No)
            .Select(r => r.No)
            .FirstOrDefaultAsync();

        var nextSeq = lastNo is null
            ? 1
            : int.Parse(lastNo.AsSpan(prefix.Length), CultureInfo.InvariantCulture) + 1;
        return DepositNumber.Format(year, nextSeq);
    }

    /// <summary>Usage kutusunun tanıdığı yarıçap politikaları (PDF bunlara göre metin seçer).</summary>
    private static readonly HashSet<string> RadiusPolicies =
        new(StringComparer.Ordinal) { "bangkok-only", "within-150", "unlimited" };

    private readonly record struct ValidationResult(
        IResult? Error,
        long AmountSatang,
        string? CustomerName,
        string? Phone,
        string? TaxId,
        string? VehicleModel,
        string? VehicleColor,
        string? Plate,
        string? ReferenceNo,
        string? FuelLevel,
        int LimitKmPerDay,
        int LimitRadiusKm,
        int DailyKm,
        string? RadiusPolicy);

    private static ValidationResult Validate(SaveDepositReceiptRequest request)
    {
        if (request.Date == default)
            return Fail("Tarih zorunludur (örn: 2026-07-03).");

        var customer = request.CustomerName?.Trim();
        if (string.IsNullOrEmpty(customer))
            return Fail("Müşteri adı zorunludur.");

        var vehicle = request.VehicleModel?.Trim();
        if (string.IsNullOrEmpty(vehicle))
            return Fail("Araç modeli zorunludur.");

        var plate = request.Plate?.Trim();
        if (string.IsNullOrEmpty(plate))
            return Fail("Plaka zorunludur.");

        if (!BahtParser.TryParse(request.Amount, out var amountSatang, out var amountError))
            return Fail(amountError);

        if (request.HandoverAt == default)
            return Fail("Teslim tarihi/saati zorunludur.");

        if (request.ReturnExpectedAt == default)
            return Fail("Beklenen iade tarihi/saati zorunludur.");

        var perDay = request.LimitKmPerDay ?? 150;
        var radius = request.LimitRadiusKm ?? 150;
        if (perDay <= 0 || radius <= 0)
            return Fail("Kullanım limitleri sıfırdan büyük olmalıdır.");

        // Araç seçiminden gelen usage değerleri (boş gelirse eski sabit davranış: 150 / within-150).
        var dailyKm = request.DailyKm ?? 150;
        if (dailyKm <= 0)
            return Fail("Günlük km limiti sıfırdan büyük olmalıdır.");

        var policy = request.RadiusPolicy?.Trim();
        policy = string.IsNullOrEmpty(policy) ? "within-150" : policy;
        if (!RadiusPolicies.Contains(policy))
            return Fail("Geçersiz yarıçap politikası (bangkok-only | within-150 | unlimited).");

        var phone = request.Phone?.Trim();
        var taxId = request.TaxId?.Trim();
        var reference = request.ReferenceNo?.Trim();
        var color = request.VehicleColor?.Trim();
        var fuel = request.FuelLevel?.Trim();

        return new ValidationResult(
            null, amountSatang, customer,
            string.IsNullOrEmpty(phone) ? null : phone,
            string.IsNullOrEmpty(taxId) ? null : taxId,
            vehicle,
            string.IsNullOrEmpty(color) ? null : color,
            plate,
            string.IsNullOrEmpty(reference) ? null : reference,
            string.IsNullOrEmpty(fuel) ? "Full" : fuel,
            perDay, radius, dailyKm, policy);

        static ValidationResult Fail(string message) =>
            new(Results.BadRequest(new ErrorResponse(message)),
                0, null, null, null, null, null, null, null, null, 0, 0, 0, null);
    }

    private static DepositReceiptResponse ToResponse(DepositReceipt r) =>
        new(r.Id, r.No, r.Date, r.CustomerName, r.Phone, r.TaxId, r.VehicleModel, r.VehicleColor, r.Plate,
            r.AmountSatang, r.PaymentMethod, r.ReferenceNo, r.FuelLevel, r.HandoverAt, r.ReturnExpectedAt,
            r.LimitKmPerDay, r.LimitRadiusKm, r.DailyKm, r.RadiusPolicy, r.CreatedAt);
}
