namespace Kasa.Api.Contracts;

public record SaveFleetSnapshotRequest(int TotalBikes, int BrokenBikes, int RentedBikes);

/// <summary>Filo günü. RentalPercent null olabilir: kiralanabilir bisiklet yoksa yüzde tanımsızdır.</summary>
public record FleetSnapshotResponse(
    DateOnly Date,
    int TotalBikes,
    int BrokenBikes,
    int RentedBikes,
    decimal? RentalPercent,
    int IdleBikes,
    bool BrokenAlert);

/// <summary>Günlük rapora gömülen filo özeti (tarih raporun kendisinde).</summary>
public record DailyFleetResponse(
    int TotalBikes,
    int BrokenBikes,
    int RentedBikes,
    decimal? RentalPercent,
    int IdleBikes,
    bool BrokenAlert);

public record FleetMonthSummaryResponse(
    decimal? AvgRentalPercent,
    int TotalBrokenDays,
    int MissingDays);

public record FleetMonthResponse(
    string Month,
    IReadOnlyList<FleetSnapshotResponse> Days,
    FleetMonthSummaryResponse Summary);
