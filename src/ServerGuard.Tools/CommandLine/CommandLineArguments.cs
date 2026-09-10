namespace ServerGuard.Tools.CommandLine;

/// <summary>
/// <c>--ad deger</c> ve <c>--bayrak</c> biçimindeki argümanları okur.
/// </summary>
/// <remarks>
/// Harici bir paket yerine bu küçük ayrıştırıcı kullanılır: aracın ihtiyacı birkaç adlandırılmış
/// değerden ibarettir ve bağımlılık eklemek, aracın her sunucuya taşınmasını zorlaştırır.
/// </remarks>
public sealed class CommandLineArguments
{
    private const string OptionPrefix = "--";

    private readonly Dictionary<string, string?> _values;

    private CommandLineArguments(string commandName, Dictionary<string, string?> values)
    {
        CommandName = commandName;
        _values = values;
    }

    /// <summary>İlk konumsal argüman: çalıştırılacak komut. Verilmemişse boş dize.</summary>
    public string CommandName { get; }

    public static CommandLineArguments Parse(string[] args)
    {
        var commandName = args.Length > 0 && !args[0].StartsWith(OptionPrefix, StringComparison.Ordinal)
            ? args[0]
            : string.Empty;

        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);

        for (var index = commandName.Length == 0 ? 0 : 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (!argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = argument[OptionPrefix.Length..];
            var hasValue = index + 1 < args.Length &&
                           !args[index + 1].StartsWith(OptionPrefix, StringComparison.Ordinal);

            values[name] = hasValue ? args[++index] : null;
        }

        return new CommandLineArguments(commandName, values);
    }

    public bool HasFlag(string name) => _values.ContainsKey(name);

    public string? GetValue(string name) => _values.GetValueOrDefault(name);

    /// <summary>
    /// Değeri önce argümandan, yoksa ortam değişkeninden okur. Sırlar komut satırında
    /// bırakılmak zorunda kalmasın diye ortam değişkeni desteklenir.
    /// </summary>
    public string? GetValueOrEnvironment(string name, string environmentVariableName)
    {
        var value = GetValue(name);

        return string.IsNullOrWhiteSpace(value)
            ? Environment.GetEnvironmentVariable(environmentVariableName)
            : value;
    }
}
