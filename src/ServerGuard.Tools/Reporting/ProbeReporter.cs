using System.Text.Json;
using ServerGuard.Tools.Probing;

namespace ServerGuard.Tools.Reporting;

/// <summary>
/// Kontrol sonuçlarını konsola yazar.
/// </summary>
/// <remarks>
/// İki biçim desteklenir: insan için renkli metin, otomasyon için JSON. Aynı sonuç kümesi
/// her iki biçimde de aynı bilgiyi taşır; biçim yalnızca sunumu değiştirir.
/// </remarks>
public static class ProbeReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void WriteText(Uri baseAddress, IReadOnlyList<ProbeStepResult> results)
    {
        Console.WriteLine();
        Console.WriteLine($"ServerGuard kontrolu  ->  {baseAddress}");
        Console.WriteLine($"Zaman                 ->  {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine(new string('-', 78));

        foreach (var result in results)
        {
            WriteLine(result);
        }

        Console.WriteLine(new string('-', 78));
        WriteSummary(results);
        Console.WriteLine();
    }

    public static void WriteJson(Uri baseAddress, IReadOnlyList<ProbeStepResult> results)
    {
        var payload = new
        {
            baseAddress = baseAddress.ToString(),
            checkedAt = DateTimeOffset.Now,
            healthy = !HasFailure(results),
            failed = Count(results, ProbeOutcome.Failed),
            warnings = Count(results, ProbeOutcome.Warning),
            steps = results.Select(result => new
            {
                name = result.Name,
                outcome = result.Outcome.ToString(),
                detail = result.Detail
            })
        };

        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>
    /// Herhangi bir kontrol başarısızsa <c>true</c>. Uyarılar sistemi başarısız saymaz;
    /// dikkat gerektirir ama çalışmayı engellemez.
    /// </summary>
    public static bool HasFailure(IReadOnlyList<ProbeStepResult> results) =>
        results.Any(result => result.Outcome == ProbeOutcome.Failed);

    private static void WriteLine(ProbeStepResult result)
    {
        var (label, color) = Describe(result.Outcome);

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write($"  [{label}] ");
        Console.ForegroundColor = previous;

        Console.WriteLine($"{result.Name.PadRight(38)} {result.Detail}");
    }

    private static void WriteSummary(IReadOnlyList<ProbeStepResult> results)
    {
        var failed = Count(results, ProbeOutcome.Failed);
        var warnings = Count(results, ProbeOutcome.Warning);
        var skipped = Count(results, ProbeOutcome.Skipped);
        var passed = Count(results, ProbeOutcome.Passed);

        Console.WriteLine(
            $"  Basarili: {passed}   Uyari: {warnings}   Basarisiz: {failed}   Atlanan: {skipped}");

        var previous = Console.ForegroundColor;

        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  SONUC: Sorun var. Yukaridaki basarisiz satirlara bakin.");
        }
        else if (warnings > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  SONUC: Sistem calisiyor, dikkat edilmesi gereken noktalar var.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  SONUC: Her sey yolunda.");
        }

        Console.ForegroundColor = previous;
    }

    private static int Count(IReadOnlyList<ProbeStepResult> results, ProbeOutcome outcome) =>
        results.Count(result => result.Outcome == outcome);

    private static (string Label, ConsoleColor Color) Describe(ProbeOutcome outcome) => outcome switch
    {
        ProbeOutcome.Passed => ("  OK  ", ConsoleColor.Green),
        ProbeOutcome.Warning => (" UYARI", ConsoleColor.Yellow),
        ProbeOutcome.Failed => (" HATA ", ConsoleColor.Red),
        _ => (" ATLA ", ConsoleColor.DarkGray)
    };
}
