namespace Kasa.Api.Contracts;

/// <summary>Rezervasyon sayaçları OPSİYONEL (Faz 11): eski istek gövdesi (üç alan) çalışmaya
/// devam eder, gönderilmeyen sayaç null yazılır. null = "girilmedi" (K2).</summary>
public record SaveFleetSnapshotRequest(
    int TotalBikes,
    int BrokenBikes,
    int RentedBikes,
    int? StartedReservations = null,
    int? EndedReservations = null);

/// <summary>Filo günü. RentalPercent null olabilir: kiralanabilir bisiklet yoksa yüzde tanımsızdır.
/// Rezervasyon sayaçları Faz 6/7 deseniyle SONA eklenmiştir (geriye dönük uyumlu).</summary>
public record FleetSnapshotResponse(
    DateOnly Date,
    int TotalBikes,
    int BrokenBikes,
    int RentedBikes,
    decimal? RentalPercent,
    int IdleBikes,
    bool BrokenAlert,
    int? StartedReservations,
    int? EndedReservations);

/// <summary>Günlük rapora gömülen filo özeti (tarih raporun kendisinde).</summary>
public record DailyFleetResponse(
    int TotalBikes,
    int BrokenBikes,
    int RentedBikes,
    decimal? RentalPercent,
    int IdleBikes,
    bool BrokenAlert,
    int? StartedReservations,
    int? EndedReservations);

/// <summary>TotalStarted/TotalEnded: null günler toplama katılmaz; TÜM günler null ise
/// toplam da null (server toplar — I1, UI toplamaz).</summary>
public record FleetMonthSummaryResponse(
    decimal? AvgRentalPercent,
    int TotalBrokenDays,
    int MissingDays,
    int? TotalStarted,
    int? TotalEnded);

public record FleetMonthResponse(
    string Month,
    IReadOnlyList<FleetSnapshotResponse> Days,
    FleetMonthSummaryResponse Summary);
