namespace FellowOakDicom.PureCodecs.Tools;

public sealed class ToolOptions
{
    private ToolOptions(string? inputPath, string? outputDirectory, bool showHelp)
    {
        InputPath = inputPath;
        OutputDirectory = outputDirectory;
        ShowHelp = showHelp;
    }

    public string? InputPath { get; }

    public string? OutputDirectory { get; }

    public bool ShowHelp { get; }

    public static ToolOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new ToolOptions(inputPath: null, outputDirectory: null, showHelp: true);
        }

        var index = 0;
        if (IsHelp(args[index]))
        {
            return new ToolOptions(inputPath: null, outputDirectory: null, showHelp: true);
        }

        string? inputPath = null;
        string? outputDirectory = null;
        while (index < args.Count)
        {
            var arg = args[index];
            if (IsHelp(arg))
            {
                return new ToolOptions(inputPath: null, outputDirectory: null, showHelp: true);
            }

            if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count)
                {
                    throw new ArgumentException("--output-dir requires a directory path.");
                }

                outputDirectory = TrimShellQuotes(args[index]);
                index++;
                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option {arg}.");
            }

            if (inputPath is not null)
            {
                throw new ArgumentException("Only one input file can be compressed at a time.");
            }

            inputPath = arg;
            index++;
        }

        return new ToolOptions(inputPath, outputDirectory, showHelp: false);
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "/?", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimShellQuotes(string value)
    {
        return value.Trim().Trim('"', '\'');
    }
}
