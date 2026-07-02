using System.Globalization;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Kasa.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Endpoints;

public static class FleetEndpoints
{
    public static void MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fleet");

        group.MapGet("/month", async (KasaDbContext db, TimeProvider clock, string? month) =>
        {
            if (month is null || !DateOnly.TryParseExact(
                    month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
                return Results.BadRequest(new ErrorResponse(
                    "month parametresi zorunludur ve YYYY-MM biçiminde olmalıdır (örn: ?month=2026-07)."));

            return Results.Ok(await BuildMonthAsync(db, clock, month, monthStart));
        });

        group.MapGet("/{date}", async (KasaDbContext db, DateOnly date) =>
        {
            var snapshot = await db.FleetSnapshots.AsNoTracking().SingleOrDefaultAsync(f => f.Date == date);
            return snapshot is null
                ? Results.NotFound(new ErrorResponse("O güne ait filo kaydı bulunamadı."))
                : Results.Ok(ToResponse(snapshot));
        });

        group.MapPut("/{date}", async (KasaDbContext db, DateOnly date, SaveFleetSnapshotRequest request) =>
        {
            var error = Validate(request);
            if (error is not null)
                return error;

            // Upsert: Date UNIQUE — aynı güne ikinci PUT kayıt sayısını artırmaz, günceller.
            var snapshot = await db.FleetSnapshots.SingleOrDefaultAsync(f => f.Date == date);
            if (snapshot is null)
            {
                snapshot = new FleetSnapshot { Date = date };
                db.FleetSnapshots.Add(snapshot);
            }

            snapshot.TotalBikes = request.TotalBikes;
            snapshot.BrokenBikes = request.BrokenBikes;
            snapshot.RentedBikes = request.RentedBikes;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(snapshot));
        });
    }

    /// <summary>
    /// Filo ay özetinin tek hesap yolu: /api/fleet/month (JSON) ve Excel "Ay Özeti" sayfası
    /// aynı sonucu buradan alır (I1).
    /// </summary>
    internal static async Task<FleetMonthResponse> BuildMonthAsync(
        KasaDbContext db, TimeProvider clock, string month, DateOnly monthStart)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var snapshots = await db.FleetSnapshots.AsNoTracking()
            .Where(f => f.Date >= monthStart && f.Date <= monthEnd)
            .OrderBy(f => f.Date)
            .ToListAsync();

        var days = snapshots.Select(ToResponse).ToList();

        // Eksik gün: ay başından bugüne kadar (bugün dahil) snapshot girilmemiş günler.
        // Gelecek günler eksik sayılmaz; ay tamamen gelecekteyse 0.
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var cutoff = monthEnd < today ? monthEnd : today;
        var missingDays = cutoff < monthStart
            ? 0
            : cutoff.DayNumber - monthStart.DayNumber + 1 - snapshots.Count(s => s.Date <= cutoff);

        return new FleetMonthResponse(month, days, new FleetMonthSummaryResponse(
            FleetCalculator.AverageRentalPercent(days.Select(d => d.RentalPercent)),
            snapshots.Sum(s => s.BrokenBikes),
            missingDays));
    }

    private static IResult? Validate(SaveFleetSnapshotRequest request)
    {
        if (request.TotalBikes < 0 || request.BrokenBikes < 0 || request.RentedBikes < 0)
            return Results.BadRequest(new ErrorResponse("Bisiklet sayıları negatif olamaz."));

        if (request.BrokenBikes + request.RentedBikes > request.TotalBikes)
            return Results.BadRequest(new ErrorResponse(
                "Arızalı ve kiradaki bisiklet toplamı filo toplamını aşamaz."));

        return null;
    }

    private static FleetSnapshotResponse ToResponse(FleetSnapshot s) =>
        new(s.Date, s.TotalBikes, s.BrokenBikes, s.RentedBikes,
            FleetCalculator.RentalPercent(s.TotalBikes, s.BrokenBikes, s.RentedBikes),
            FleetCalculator.IdleBikes(s.TotalBikes, s.BrokenBikes, s.RentedBikes),
            s.BrokenBikes > 0);

    /// <summary>Günlük rapora gömülen filo nesnesi (ReportEndpoints kullanır).</summary>
    internal static DailyFleetResponse ToDailyResponse(FleetSnapshot s) =>
        new(s.TotalBikes, s.BrokenBikes, s.RentedBikes,
            FleetCalculator.RentalPercent(s.TotalBikes, s.BrokenBikes, s.RentedBikes),
            FleetCalculator.IdleBikes(s.TotalBikes, s.BrokenBikes, s.RentedBikes),
            s.BrokenBikes > 0);
}
