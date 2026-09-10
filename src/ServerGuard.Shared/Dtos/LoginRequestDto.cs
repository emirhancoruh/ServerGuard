namespace ServerGuard.Shared.Dtos;

/// <summary>Panel oturumu açma isteği. Doğrulama API tarafında FluentValidation ile yapılır.</summary>
public sealed record LoginRequestDto(string UserName, string Password);
