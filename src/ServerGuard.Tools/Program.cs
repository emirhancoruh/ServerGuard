using ServerGuard.Tools.CommandLine;
using ServerGuard.Tools.Commands;

// ServerGuard yardimci araci: kurulum sirlarini uretir ve calisan bir API'yi disaridan
// dogrular. Panelin veya agent'in calismasi icin gerekli degildir; bagimsiz calisir.

const int UsageExitCode = 64;
const int CancelledExitCode = 130;

var arguments = CommandLineArguments.Parse(args);

if (string.IsNullOrEmpty(arguments.CommandName) || arguments.HasFlag("help"))
{
    WriteUsage();
    return UsageExitCode;
}

var command = CommandRegistry.Find(arguments.CommandName);

if (command is null)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {arguments.CommandName}");
    WriteUsage();
    return UsageExitCode;
}

// Ctrl+C ile temiz cikis: calisan istek iptal edilir, surec asili kalmaz.
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    return await command.RunAsync(arguments, cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Islem iptal edildi.");
    return CancelledExitCode;
}

static void WriteUsage()
{
    Console.WriteLine();
    Console.WriteLine("ServerGuard.Tools - kurulum ve saglik dogrulama araci");
    Console.WriteLine();
    Console.WriteLine("Komutlar:");

    foreach (var command in CommandRegistry.All)
    {
        Console.WriteLine($"  {command.Name.PadRight(16)} {command.Description}");
        Console.WriteLine($"  {new string(' ', 16)} {command.Usage}");
        Console.WriteLine();
    }
}
