using BenchmarkDotNet.Running;

namespace FellowOakDicom.PureCodecs.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--verify", StringComparison.Ordinal))
        {
            BenchmarkFixtureVerifier.Verify();
            return;
        }

        BenchmarkRunner.Run<CodecBenchmarks>(args: args);
    }
}
