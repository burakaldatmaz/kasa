using Kasa.Domain;

namespace Kasa.Api.Data;

/// <summary>Kategori persistence modeli. Domain saf kalır (I2); EF konfigürasyonu KasaDbContext'te.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TransactionType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
