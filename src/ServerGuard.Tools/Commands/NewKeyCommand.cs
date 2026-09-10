using ServerGuard.Shared.Security;
using ServerGuard.Tools.CommandLine;

namespace ServerGuard.Tools.Commands;

/// <summary>
/// Agent için yeni bir API anahtarı üretir ve nereye yazılacağını gösterir.
/// </summary>
/// <remarks>
/// Her sunucuya ayrı anahtar verilmesi önerilir: biri sızarsa yalnızca o anahtar iptal edilir,
/// diğer sunucular veri göndermeye devam eder.
/// </remarks>
public sealed class NewKeyCommand : ICommand
{
    private const string NameOption = "name";
    private const string IndexOption = "index";
    private const string DefaultKeyName = "sunucu";
    private const string DefaultIndex = "0";

    public string Name => "new-key";

    public string Description => "Agent icin rastgele bir API anahtari uretir.";

    public string Usage => "ServerGuard.Tools new-key [--name SERVER10] [--index 0]";

    public Task<int> RunAsync(CommandLineArguments arguments, CancellationToken cancellationToken)
    {
        var keyName = arguments.GetValue(NameOption) ?? DefaultKeyName;
        var index = arguments.GetValue(IndexOption) ?? DefaultIndex;
        var key = SecretGenerator.Create();

        Console.WriteLine();
        Console.WriteLine("API tarafina (user-secrets veya ortam degiskeni):");
        Console.WriteLine($"  Security__Ingest__ApiKeys__{index}__Name={keyName}");
        Console.WriteLine($"  Security__Ingest__ApiKeys__{index}__Key={key}");
        Console.WriteLine();
        Console.WriteLine("Agent tarafina (o sunucudaki appsettings.json):");
        Console.WriteLine($"  \"Agent\": {{ \"ApiKey\": \"{key}\" }}");
        Console.WriteLine();
        Console.WriteLine("Anahtari kopyaladiktan sonra bu ekrani kapatin; log'a veya sohbete yapistirmayin.");
        Console.WriteLine();

        return Task.FromResult(0);
    }
}
