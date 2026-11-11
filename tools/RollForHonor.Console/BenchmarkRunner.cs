using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RollForHonor.ConsoleHost;

internal sealed record BenchmarkSample(double Milliseconds, double OperationsPerSecond,
    double ResolutionsPerSecond, double AllocatedBytesPerOperation, int Gen0, int Gen1, int Gen2, long Checksum);

internal sealed record BenchmarkResult(string Scenario, int ResolutionsPerOperation, BenchmarkSample[] Samples);

internal static class BenchmarkRunner
{
    public static object Run(string scenario, int iterations, int warmup, int samples, int seed)
    {
        var results = new List<BenchmarkResult>();
        foreach (var name in scenario == "all" ? CombatScenarios.Names : [scenario])
        {
            var measured = new List<BenchmarkSample>();
            for (var sample = 0; sample < samples; sample++)
            {
                // Identical seed and warmup count make the workload comparable across samples.
                var (run, resolutions) = new CombatScenarios(seed).Prepare(name);
                for (var i = 0; i < warmup; i++) run();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var gc0 = GC.CollectionCount(0);
                var gc1 = GC.CollectionCount(1);
                var gc2 = GC.CollectionCount(2);
                var started = Stopwatch.GetTimestamp();
                long checksum = 0;
                for (var i = 0; i < iterations; i++) checksum += run();
                var elapsed = Stopwatch.GetElapsedTime(started);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                measured.Add(new BenchmarkSample(elapsed.TotalMilliseconds, iterations / elapsed.TotalSeconds,
                    (double)iterations * resolutions / elapsed.TotalSeconds, (double)allocated / iterations,
                    GC.CollectionCount(0) - gc0, GC.CollectionCount(1) - gc1, GC.CollectionCount(2) - gc2, checksum));
            }
            var result = new BenchmarkResult(name, name == "sequence" ? 4 : 1, measured.ToArray());
            results.Add(result);
            var rates = measured.Select(value => value.ResolutionsPerSecond).Order().ToArray();
            var median = rates.Length % 2 == 0 ? (rates[rates.Length / 2 - 1] + rates[rates.Length / 2]) / 2 : rates[rates.Length / 2];
            Console.WriteLine($"{name}: median {median:N0} resolutions/s; range {rates.First():N0}..{rates.Last():N0}; " +
                $"allocated {measured.Average(value => value.AllocatedBytesPerOperation):N0} B/op; {result.ResolutionsPerOperation} resolution(s)/op");
        }
        return new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
#if DEBUG
            Configuration = "Debug",
#else
            Configuration = "Release",
#endif
            IterationsPerSample = iterations, WarmupPerSample = warmup, Seed = seed,
            Scope = "Single-threaded Domain harness; includes request creation, dice/step traces and finalization. No Godot, Application commit or rendering.",
            Results = results
        };
    }
}
