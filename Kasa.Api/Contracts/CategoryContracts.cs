using Kasa.Domain;

namespace Kasa.Api.Contracts;

public record CreateCategoryRequest(string? Name, TransactionType Type);

/// <summary>Sadece gönderilen alanlar güncellenir; null alan dokunulmaz bırakılır.</summary>
public record UpdateCategoryRequest(string? Name, int? SortOrder, bool? IsActive);

public record CategoryResponse(int Id, string Name, TransactionType Type, bool IsActive, int SortOrder);
