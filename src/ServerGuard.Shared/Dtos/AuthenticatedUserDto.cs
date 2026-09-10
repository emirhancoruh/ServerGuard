namespace ServerGuard.Shared.Dtos;

/// <summary>Geçerli token'ın sahibi. Panelin oturumu doğrulaması için kullanılır.</summary>
public sealed record AuthenticatedUserDto(string UserName, DateTimeOffset ExpiresAt);
