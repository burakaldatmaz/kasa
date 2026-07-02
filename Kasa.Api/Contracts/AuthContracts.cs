namespace Kasa.Api.Contracts;

public record LoginRequest(string? Password);

public record MeResponse(bool Authenticated);
