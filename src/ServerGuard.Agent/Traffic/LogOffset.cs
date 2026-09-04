namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Kalıcı olarak saklanan okuma konumu: hangi dosyanın neresine kadar okunduğu.
/// </summary>
public sealed record LogOffset(string FileName, long Offset);
