namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Başarılı oturum açma yanıtı. Token yalnızca burada döner; sunucu tarafında saklanmaz.
/// </summary>
public sealed record LoginResponseDto(string AccessToken, DateTimeOffset ExpiresAt, string UserName);
