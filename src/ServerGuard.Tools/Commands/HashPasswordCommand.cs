using ServerGuard.Shared;
using ServerGuard.Shared.Security;
using ServerGuard.Tools.CommandLine;

namespace ServerGuard.Tools.Commands;

/// <summary>
/// Panel kullanıcısı için parola özeti üretir.
/// </summary>
/// <remarks>
/// Parola varsayılan olarak ekrana yazılmadan sorulur; komut satırında verilirse kabuk
/// geçmişinde ve süreç listesinde görünür hale gelir. Bu yüzden <c>--password</c> yalnızca
/// betikten çağrılırken kullanılmalıdır.
/// </remarks>
public sealed class HashPasswordCommand : ICommand
{
    private const string PasswordOption = "password";
    private const string UserOption = "user";
    private const string DefaultUserName = "admin";

    public string Name => "hash-password";

    public string Description => "Panel kullanicisi icin PBKDF2 parola ozeti uretir.";

    public string Usage => "ServerGuard.Tools hash-password [--user admin] [--password <parola>]";

    public Task<int> RunAsync(CommandLineArguments arguments, CancellationToken cancellationToken)
    {
        var userName = arguments.GetValue(UserOption) ?? DefaultUserName;
        var password = arguments.GetValue(PasswordOption) ?? PasswordPrompt.Read("Parola: ");

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Parola bos olamaz.");
            return Task.FromResult(1);
        }

        if (password.Length < AuthConstraints.MinimumPasswordLength)
        {
            Console.Error.WriteLine(
                $"Parola en az {AuthConstraints.MinimumPasswordLength} karakter olmali.");
            return Task.FromResult(1);
        }

        var hash = PasswordHash.Create(password);

        Console.WriteLine();
        Console.WriteLine("appsettings / user-secrets icin:");
        Console.WriteLine($"  Security:Panel:Users:0:UserName     = {userName}");
        Console.WriteLine($"  Security:Panel:Users:0:PasswordHash = {hash}");
        Console.WriteLine();
        Console.WriteLine("Ortam degiskeni olarak (production):");
        Console.WriteLine($"  Security__Panel__Users__0__UserName={userName}");
        Console.WriteLine($"  Security__Panel__Users__0__PasswordHash={hash}");
        Console.WriteLine();

        return Task.FromResult(0);
    }
}
