namespace ServerGuard.Tools.Commands;

/// <summary>Araçtaki tüm komutların kaydı.</summary>
public static class CommandRegistry
{
    public static IReadOnlyList<ICommand> All { get; } =
    [
        new CheckCommand(),
        new HashPasswordCommand(),
        new NewKeyCommand()
    ];

    public static ICommand? Find(string name) =>
        All.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));
}
