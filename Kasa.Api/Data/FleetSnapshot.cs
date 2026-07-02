namespace Kasa.Api.Data;

/// <summary>Bir günün filo durumu. Gün başına tek kayıt (Date UNIQUE), PUT upsert eder.</summary>
public class FleetSnapshot
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TotalBikes { get; set; }
    public int BrokenBikes { get; set; }
    public int RentedBikes { get; set; }
}
