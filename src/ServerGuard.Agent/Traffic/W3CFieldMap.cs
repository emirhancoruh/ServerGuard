namespace ServerGuard.Agent.Traffic;

/// <summary>
/// "#Fields:" yönergesinden çıkarılan alan adı → sütun indeksi eşlemesi.
/// IIS log yapılandırması değiştiğinde dosyanın ortasında yeni bir yönerge yazabilir;
/// bu yüzden eşleme okuma sırasında güncellenebilir olmalıdır.
/// </summary>
public sealed class W3CFieldMap
{
    public const string DirectivePrefix = "#Fields:";

    /// <summary>Yorum ve yönerge satırlarının başladığı karakter.</summary>
    public const char CommentPrefix = '#';

    private readonly Dictionary<string, int> _indexes;

    private W3CFieldMap(Dictionary<string, int> indexes) => _indexes = indexes;

    public int FieldCount => _indexes.Count;

    /// <summary>
    /// "#Fields: date time c-ip ..." satırını eşlemeye çevirir.
    /// Satır bir alan yönergesi değilse veya hiç alan içermiyorsa null döner.
    /// </summary>
    public static W3CFieldMap? TryCreate(string line)
    {
        if (!line.StartsWith(DirectivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var names = line[DirectivePrefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (names.Length == 0)
        {
            return null;
        }

        var indexes = new Dictionary<string, int>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < names.Length; index++)
        {
            // Aynı alan iki kez bildirilirse ilk sütun geçerli sayılır.
            indexes.TryAdd(names[index], index);
        }

        return new W3CFieldMap(indexes);
    }

    /// <summary>Alanın sütun indeksini döner; alan bildirilmemişse -1.</summary>
    public int IndexOf(string field) => _indexes.GetValueOrDefault(field, -1);
}
