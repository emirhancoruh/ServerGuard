using ServerGuard.Shared.Security;
using ServerGuard.Tools.CommandLine;

namespace ServerGuard.Tools.Commands;

/// <summary>
/// Panel oturum token'larının imzalanacağı anahtarı üretir.
/// </summary>
/// <remarks>
/// Agent anahtarından ayrı bir komuttur çünkü gittiği yer farklıdır. Tek komut olsaydı
/// çıktı ya agent değişkenlerini gösterip imza anahtarını yanlış yere yazdırır ya da
/// kullanıcıyı iki farklı yoldan birini seçmeye zorlardı; ikisi de kurulum sırasında
/// sessizce yanlış yapılandırmaya yol açar.
/// </remarks>
public sealed class NewSigningKeyCommand : ICommand
{
    public string Name => "new-signing-key";

    public string Description => "Panel oturum token'lari icin imza anahtari uretir.";

    public string Usage => "ServerGuard.Tools new-signing-key";

    public Task<int> RunAsync(CommandLineArguments arguments, CancellationToken cancellationToken)
    {
        var key = SecretGenerator.Create();

        Console.WriteLine();
        Console.WriteLine("API tarafina (user-secrets veya ortam degiskeni):");
        Console.WriteLine($"  Security__Jwt__SigningKey={key}");
        Console.WriteLine();
        Console.WriteLine("Bu anahtar YALNIZCA API'de bulunur; agent'lara veya panele verilmez.");
        Console.WriteLine("Degistirilirse acik olan tum panel oturumlari duser - erisimi acilen");
        Console.WriteLine("kesmek gerekirse yontem budur.");
        Console.WriteLine();
        Console.WriteLine("Anahtari kopyaladiktan sonra bu ekrani kapatin; log'a veya sohbete yapistirmayin.");
        Console.WriteLine();

        return Task.FromResult(0);
    }
}
