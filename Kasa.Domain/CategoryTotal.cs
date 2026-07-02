namespace Kasa.Domain;

/// <summary>Kategori toplamı girdisi: kategori adı + satır tutarı (satang).</summary>
public readonly record struct CategoryAmount(string Category, long AmountSatang);

/// <summary>Bir kategorinin hesaplanmış toplamı.</summary>
public readonly record struct CategoryTotal(string Category, Money Total);
