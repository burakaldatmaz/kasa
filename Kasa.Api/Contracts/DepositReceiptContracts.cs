using Kasa.Domain;

namespace Kasa.Api.Contracts;

/// <summary>
/// Depozito makbuzu oluşturma isteği. Amount BAHT string'dir ("3000" / "3000.50") — satang'a
/// çeviri server'da (BahtParser, I1). No server'da atanır, istemci göndermez.
/// FuelLevel/limitler boş gelirse varsayılanlanır (Full · 150 · 150 · within-150).
/// Phone/TaxId/ReferenceNo opsiyoneldir; boş string server'da null'a normalize edilir.
/// DailyKm + RadiusPolicy seçilen araçtan gelir; boş gelirse 150 / within-150 varsayılır.
/// </summary>
public record SaveDepositReceiptRequest(
    DateOnly Date,
    string? CustomerName,
    string? Phone,
    string? TaxId,
    string? VehicleModel,
    string? VehicleColor,
    string? Plate,
    string? Amount,
    PaymentMethod PaymentMethod,
    string? ReferenceNo,
    string? FuelLevel,
    DateTime HandoverAt,
    DateTime ReturnExpectedAt,
    int? LimitKmPerDay,
    int? LimitRadiusKm,
    int? DailyKm,
    string? RadiusPolicy);

public record DepositReceiptResponse(
    int Id,
    string No,
    DateOnly Date,
    string CustomerName,
    string? Phone,
    string? TaxId,
    string VehicleModel,
    string? VehicleColor,
    string Plate,
    long AmountSatang,
    PaymentMethod PaymentMethod,
    string? ReferenceNo,
    string FuelLevel,
    DateTime HandoverAt,
    DateTime ReturnExpectedAt,
    int LimitKmPerDay,
    int LimitRadiusKm,
    int DailyKm,
    string RadiusPolicy,
    DateTime CreatedAt);
