using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Msys2Manager.CLI.Commands;

public static class CommandExtensions
{
    public static void AddCommands(this RootCommand rootCommand, IServiceProvider services)
    {
        var bootstrap = new BootstrapCommand();
        bootstrap.SetHandler(() => services.GetRequiredService<BootstrapCommand.Handler>().InvokeAsync,
            bootstrap.GetOption("--url"));
        rootCommand.AddCommand(bootstrap);

        var update = new UpdateCommand();
        update.SetHandler(() => services.GetRequiredService<UpdateCommand.Handler>().InvokeAsync);
        rootCommand.AddCommand(update);

        var sync = new SyncCommand();
        sync.SetHandler(() => services.GetRequiredService<SyncCommand.Handler>().InvokeAsync,
            sync.GetOption("--prune"));
        rootCommand.AddCommand(sync);

        var add = new AddCommand();
        add.SetHandler(() => services.GetRequiredService<AddCommand.Handler>().InvokeAsync,
            add.GetArgument("packages"),
            add.GetOption("--version"));
        rootCommand.AddCommand(add);

        var remove = new RemoveCommand();
        remove.SetHandler(() => services.GetRequiredService<RemoveCommand.Handler>().InvokeAsync,
            remove.GetArgument("packages"));
        rootCommand.AddCommand(remove);

        var run = new RunCommand();
        run.SetHandler(() => services.GetRequiredService<RunCommand.Handler>().InvokeAsync,
            run.GetOption("--list"),
            run.GetArgument("task"));
        rootCommand.AddCommand(run);

        var shell = new ShellCommand();
        shell.SetHandler(() => services.GetRequiredService<ShellCommand.Handler>().InvokeAsync);
        rootCommand.AddCommand(shell);

        var clean = new CleanCommand();
        clean.SetHandler(() => services.GetRequiredService<CleanCommand.Handler>().InvokeAsync,
            clean.GetOption("--force"));
        rootCommand.AddCommand(clean);
    }
}
