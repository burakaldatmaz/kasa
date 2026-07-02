namespace Kasa.Api.Data;

/// <summary>Bir günün filo durumu. Gün başına tek kayıt (Date UNIQUE), PUT upsert eder.</summary>
public class FleetSnapshot
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TotalBikes { get; set; }
    public int BrokenBikes { get; set; }
    public int RentedBikes { get; set; }

    // Faz 11 operasyonel sayaçlar. null = "girilmedi", 0 = "gerçekten sıfır" (K2).
    // Filo adetleriyle matematiksel ilişki taşımazlar; kapasite kısıtına girmezler (K1).
    public int? StartedReservations { get; set; }
    public int? EndedReservations { get; set; }
}
