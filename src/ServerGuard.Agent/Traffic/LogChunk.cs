namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Dosyadan okunan tamamlanmış satırlar ve bir sonraki okumanın başlayacağı konum.
/// </summary>
/// <param name="Lines">Satır sonu görülmüş, eksiksiz satırlar.</param>
/// <param name="NextOffset">Yarım kalmış satırın başlangıcı; buradan devam edilir.</param>
public sealed record LogChunk(IReadOnlyList<string> Lines, long NextOffset)
{
    public static LogChunk Empty(long offset) => new([], offset);

    public bool HasLines => Lines.Count > 0;
}
