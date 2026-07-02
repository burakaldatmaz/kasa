using System.Globalization;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Kasa.Api.Excel;
using Kasa.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports");

        group.MapGet("/daily", async (KasaDbContext db, DateOnly? date) =>
        {
            if (date is null)
                return Results.BadRequest(new ErrorResponse("date parametresi zorunludur (örn: ?date=2026-07-02)."));

            return Results.Ok(await BuildDailyReportAsync(db, date.Value));
        });

        // PDF, JSON raporuyla AYNI hesap yolunu (BuildDailyReportAsync → Domain) kullanır;
        // PDF katmanı hazır DTO'yu yalnızca dizgiye döker (I1/I2).
        group.MapGet("/daily/pdf", async (KasaDbContext db, string? date) =>
        {
            // DateOnly binding'in gövdesiz 400'ü yerine Türkçe mesaj için string alınıp elle parse edilir.
            if (date is null || !DateOnly.TryParseExact(
                    date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return Results.BadRequest(new ErrorResponse(
                    "date parametresi zorunludur ve YYYY-AA-GG biçiminde geçerli bir tarih olmalıdır (örn: ?date=2026-08-03)."));

            var report = await BuildDailyReportAsync(db, day);
            return Results.File(
                Pdf.DailyReportPdf.Render(report), "application/pdf", $"kasa-islem-{day:yyyy-MM-dd}.pdf");
        });

        group.MapGet("/month", async (KasaDbContext db, string? month) =>
        {
            if (month is null || !TryParseMonth(month, out var monthStart))
                return Results.BadRequest(MonthParamError());

            var (report, _) = await BuildMonthReportAsync(db, month, monthStart);
            return Results.Ok(report);
        });

        // Excel, JSON ay raporuyla AYNI hesap yolunu (BuildMonthReportAsync → Domain) kullanır;
        // ClosedXML katmanı hazır DTO'ları yalnızca hücreye döker (I1/I2), formül yazılmaz.
        group.MapGet("/month/xlsx", async (KasaDbContext db, TimeProvider clock, string? month) =>
        {
            if (month is null || !TryParseMonth(month, out var monthStart))
                return Results.BadRequest(MonthParamError());

            var (report, rows) = await BuildMonthReportAsync(db, month, monthStart);
            var fleet = await FleetEndpoints.BuildMonthAsync(db, clock, month, monthStart);
            var txns = rows.Select(r => new MonthTxnRow(
                r.Date, r.Type, r.Category, r.PaymentMethod, r.AmountSatang, r.Note)).ToList();

            return Results.File(
                MonthReportExcel.Render(report, fleet, txns),
                MonthReportExcel.ContentType,
                $"kasa-{month}.xlsx");
        });
    }

    private static bool TryParseMonth(string month, out DateOnly monthStart) =>
        DateOnly.TryParseExact(
            month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out monthStart);

    private static ErrorResponse MonthParamError() =>
        new("month parametresi zorunludur ve YYYY-MM biçiminde olmalıdır (örn: ?month=2026-07).");

    /// <summary>
    /// Aylık raporun tek hesap yolu: /month (JSON) ve /month/xlsx aynı sonucu buradan alır.
    /// Ham satır listesi de döner: Excel'in "İşlemler" sayfası aynı sorgudan beslenir,
    /// ikinci bir DB okuması ve sıralama farkı oluşmaz.
    /// </summary>
    private static async Task<(MonthReportResponse Report, List<Row> Rows)> BuildMonthReportAsync(
        KasaDbContext db, string month, DateOnly monthStart)
    {
        var posFeeRate = await GetDecimalSettingAsync(db, "PosFeeRate");
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var rows = await LoadRowsAsync(db, monthStart, monthEnd);

        var chain = MonthCalculator.CalculateChain(rows.Select(ToDatedLine), posFeeRate);
        var totals = MonthCalculator.Totals(chain);
        var finalBalance = chain.Count == 0 ? Money.Zero : chain[^1].Result.ClosingBalance;

        var partner1Name = await GetSettingAsync(db, "Partner1Name");
        var partner1Share = await GetDecimalSettingAsync(db, "Partner1Share");
        var partner2Name = await GetSettingAsync(db, "Partner2Name");
        var partner2Share = await GetDecimalSettingAsync(db, "Partner2Share");
        var distribution = MonthCalculator.Split(finalBalance, partner1Share);

        var report = new MonthReportResponse(
            month,
            [.. chain.Select(d => new MonthDayResponse(
                d.Date,
                d.Result.IncomeTotal.Satang,
                d.Result.ExpenseTotal.Satang,
                d.Result.PosFee.Satang,
                d.Result.DayNet.Satang,
                d.Result.ClosingBalance.Satang))],
            ToCategoryTotals(rows.Where(r => r.Type == TransactionType.Income)),
            ToCategoryTotals(rows.Where(r => r.Type == TransactionType.Expense)),
            finalBalance.Satang,
            new DistributionResponse(
                new PartnerShareResponse(partner1Name, partner1Share * 100m, distribution.Partner1.Satang),
                new PartnerShareResponse(partner2Name, partner2Share * 100m, distribution.Partner2.Satang)),
            new MonthTotalsResponse(
                totals.IncomeTotal.Satang,
                totals.ExpenseTotal.Satang,
                totals.PosFee.Satang,
                totals.DayNet.Satang));

        return (report, rows);
    }

    /// <summary>
    /// Günlük raporun tek hesap yolu: /daily (JSON) ve /daily/pdf aynı sonucu buradan alır.
    /// Devir saklanmaz (I2): ay başından istenen güne kadarki tüm satırlar okunur,
    /// zincir her seferinde Domain'de yeniden kurulur.
    /// </summary>
    private static async Task<DailyReportResponse> BuildDailyReportAsync(KasaDbContext db, DateOnly day)
    {
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var posFeeRate = await GetDecimalSettingAsync(db, "PosFeeRate");

        var rows = await LoadRowsAsync(db, monthStart, day);

        var previousBalance = MonthCalculator.CalculatePreviousBalance(
            rows.Where(r => r.Date < day).Select(ToDatedLine), posFeeRate);

        var todayRows = rows.Where(r => r.Date == day).ToList();
        var result = DailyCalculator.Calculate(
            [.. todayRows.Select(r => ToDatedLine(r).Line)], posFeeRate, previousBalance.Satang);

        var incomeRows = todayRows.Where(r => r.Type == TransactionType.Income).ToList();
        var expenseRows = todayRows.Where(r => r.Type == TransactionType.Expense).ToList();

        var fleet = await db.FleetSnapshots.AsNoTracking().SingleOrDefaultAsync(f => f.Date == day);

        return new DailyReportResponse(
            day,
            previousBalance.Satang,
            [.. incomeRows.Select(ToLine)],
            [.. expenseRows.Select(ToLine)],
            ToCategoryTotals(incomeRows),
            ToCategoryTotals(expenseRows),
            result.IncomeTotal.Satang,
            result.ExpenseTotal.Satang,
            result.PosFee.Satang,
            posFeeRate * 100m,
            result.DayNet.Satang,
            result.ClosingBalance.Satang,
            FleetMissing: fleet is null,
            Fleet: fleet is null ? null : FleetEndpoints.ToDailyResponse(fleet));
    }

    /// <summary>Rapor sorgu satırı: hesap için gereken alanlar + görüntü alanları (Id, Note, kategori adı).</summary>
    private sealed record Row(
        int Id,
        DateOnly Date,
        TransactionType Type,
        PaymentMethod PaymentMethod,
        long AmountSatang,
        string? Note,
        string Category);

    private static async Task<List<Row>> LoadRowsAsync(KasaDbContext db, DateOnly from, DateOnly to) =>
        await db.Transactions.AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new Row(
                t.Id, t.Date, t.Type, t.PaymentMethod, t.AmountSatang, t.Note, t.Category!.Name))
            .ToListAsync();

    private static DatedTxnLine ToDatedLine(Row r) =>
        new(r.Date, new TxnLine(r.Type, r.PaymentMethod, r.AmountSatang));

    private static ReportLineResponse ToLine(Row r) =>
        new(r.Id, r.Category, r.PaymentMethod, r.Note, r.AmountSatang);

    private static IReadOnlyList<CategoryTotalResponse> ToCategoryTotals(IEnumerable<Row> rows) =>
        [.. MonthCalculator.SumByCategory(rows.Select(r => new CategoryAmount(r.Category, r.AmountSatang)))
            .Select(t => new CategoryTotalResponse(t.Category, t.Total.Satang))];

    private static async Task<string> GetSettingAsync(KasaDbContext db, string key)
    {
        // Ayarlar seed'lidir; yoksa bu bir konfigürasyon hatasıdır ve 500 doğru davranıştır.
        var setting = await db.Settings.AsNoTracking().SingleAsync(s => s.Key == key);
        return setting.Value;
    }

    private static async Task<decimal> GetDecimalSettingAsync(KasaDbContext db, string key) =>
        decimal.Parse(await GetSettingAsync(db, key), CultureInfo.InvariantCulture);
}
