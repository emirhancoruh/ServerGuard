using ServerGuard.Tools.CommandLine;

namespace ServerGuard.Tools.Commands;

/// <summary>
/// Araçtan çalıştırılabilen tek bir komut.
/// </summary>
/// <remarks>
/// Yeni bir komut eklemek için bu arayüzü uygulamak ve <see cref="CommandRegistry"/> içine
/// kaydetmek yeterlidir; <c>Program</c> değişmez.
/// </remarks>
public interface ICommand
{
    /// <summary>Komut satırında yazılan ad.</summary>
    string Name { get; }

    /// <summary>Yardım ekranında gösterilen tek satırlık açıklama.</summary>
    string Description { get; }

    /// <summary>Kullanım örneği; yardım ekranında gösterilir.</summary>
    string Usage { get; }

    /// <summary>Süreç çıkış kodunu döner: 0 başarılı, 0 dışındaki değer sorun.</summary>
    Task<int> RunAsync(CommandLineArguments arguments, CancellationToken cancellationToken);
}
