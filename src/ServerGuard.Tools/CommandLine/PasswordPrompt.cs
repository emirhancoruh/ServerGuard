using System.Text;

namespace ServerGuard.Tools.CommandLine;

/// <summary>
/// Parolayı ekrana yazmadan okur.
/// </summary>
/// <remarks>
/// Girdi yönlendirilmişse (betikten çağrı, boru hattı) tuş tuş okuma yapılamaz;
/// o durumda satır olduğu gibi okunur.
/// </remarks>
public static class PasswordPrompt
{
    public static string Read(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var builder = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                continue;
            }

            // Ok tuslari gibi karakter uretmeyen tuslar yok sayilir.
            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }
}
