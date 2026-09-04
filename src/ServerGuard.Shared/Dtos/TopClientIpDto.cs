namespace ServerGuard.Shared.Dtos;

/// <summary>En çok istek gönderen kaynak adreslerinden biri.</summary>
public sealed record TopClientIpDto(string ClientIp, int RequestCount);
