namespace Retag;

/// <summary>
/// The dispatcher, callable in-process so the test suite can drive exactly what
/// a presenter drives - same code path, same stdout, same exit codes.
/// </summary>
public static class Cli
{
    public static async Task<int> RunAsync(string[] argv)
    {
        var args = Args.Parse(argv);
        var config = Config.Load();
        var command = args.At(0);

        if (command is null || args.Has("--help") || command is "help" or "-h")
        {
            Help.Print();
            return 0;
        }

        try
        {
            return command switch
            {
                "seed" => Commands.Seed(args),
                "run" => await Run(args, config),
                "propose" => await Commands.Propose(args, config),
                "trace" => Commands.TraceShow(args),
                "traces" => Commands.TracesGroupBy(args),
                "replay" => Commands.Replay(args),
                "stop" => Commands.Stop(args),
                "release" => Commands.Release(args),
                "reset" => Commands.Reset(args),
                _ => Unknown(command),
            };
        }
        catch (FileNotFoundException)
        {
            return Fail("fixtures missing - run: retag seed");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Nothing reaches a stage with a stack trace attached to it.
            return Fail(ex.Message);
        }
    }

    private static async Task<int> Run(Args args, Config config)
    {
        // Rung 0, printed. Any run checks the flag before it starts.
        if (StopFlags.Active() is { } flag) return Commands.RefuseToStart(flag.Display, flag.Reason);

        if (args.Has("--gate")) return Commands.RunGate(args);
        if (args.Has("--canary") || args.Has("--lot")) return await Commands.RunCanary(args, config);
        return Commands.RunJob(args);
    }

    private static int Fail(string message)
    {
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Amber(message)}");
        Ui.Blank();
        Ui.Footer(1, 0);
        return 1;
    }

    private static int Unknown(string command)
    {
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Amber("unknown command: " + command)}");
        Help.Print();
        return 1;
    }
}
