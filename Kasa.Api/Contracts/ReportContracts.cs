using Kasa.Domain;

namespace Kasa.Api.Contracts;

/// <summary>Rapor satırı. Tutar satang döner, frontend 100'e böler (I1).</summary>
public record ReportLineResponse(
    int Id,
    string Category,
    PaymentMethod PaymentMethod,
    string? Note,
    long AmountSatang);

public record CategoryTotalResponse(string Category, long TotalSatang);

public record DailyReportResponse(
    DateOnly Date,
    long PreviousBalance,
    IReadOnlyList<ReportLineResponse> IncomeLines,
    IReadOnlyList<ReportLineResponse> ExpenseLines,
    IReadOnlyList<CategoryTotalResponse> IncomeByCategory,
    IReadOnlyList<CategoryTotalResponse> ExpenseByCategory,
    long IncomeTotal,
    long ExpenseTotal,
    long PosFee,
    // "POS Kesintisi (%3,5)" etiketi içindir; SharePercent gibi yüzde olarak döner (I1: oran Settings'ten).
    decimal PosFeeRatePercent,
    long DayNet,
    long ClosingBalance,
    bool FleetMissing,
    DailyFleetResponse? Fleet);

public record MonthDayResponse(
    DateOnly Date,
    long IncomeTotal,
    long ExpenseTotal,
    long PosFee,
    long DayNet,
    long CumulativeBalance);

public record PartnerShareResponse(string Name, decimal SharePercent, long AmountSatang);

public record DistributionResponse(PartnerShareResponse Partner1, PartnerShareResponse Partner2);

/// <summary>Ay alt toplam satırı (satang). PosFeeRatePercent deseni gibi Faz 7 eklentisidir:
/// alan SONA eklenir, mevcut alanlar değişmez (geriye dönük uyumlu) — UI ve Excel toplam
/// satırı bu değerleri hazır gösterir, istemci toplamaz (I1).</summary>
public record MonthTotalsResponse(long IncomeTotal, long ExpenseTotal, long PosFee, long DayNet);

public record MonthReportResponse(
    string Month,
    IReadOnlyList<MonthDayResponse> Days,
    IReadOnlyList<CategoryTotalResponse> IncomeByCategory,
    IReadOnlyList<CategoryTotalResponse> ExpenseByCategory,
    long FinalBalance,
    DistributionResponse Distribution,
    MonthTotalsResponse Totals);
