using System.Text.Json;
using RollForHonor.ConsoleHost;

if (args.Length == 0 || args is ["smoke"])
{
    try
    {
        CombatScenarios.Smoke();
        Console.WriteLine("All smoke scenarios passed.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Smoke failed: {exception.Message}");
        return 1;
    }
}

if (args is ["--help"] or ["-h"])
{
    Help();
    return 0;
}

try
{
    if (args[0] != "benchmark") throw new ArgumentException("Expected smoke or benchmark.");
    var options = new Dictionary<string, string>();
    for (var i = 1; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || args[i] is not ("--scenario" or "--iterations" or "--warmup" or "--samples" or "--seed" or "--json")
            || !options.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException($"Invalid, repeated or incomplete option '{args[i]}'.");
    }
    var scenario = options.GetValueOrDefault("--scenario", "all");
    if (scenario != "all" && !CombatScenarios.Names.Contains(scenario)) throw new ArgumentException("Unknown scenario.");
    int Number(string key, int fallback, int minimum, int maximum)
    {
        if (!options.TryGetValue(key, out var text)) return fallback;
        if (!int.TryParse(text, out var value) || value < minimum || value > maximum)
            throw new ArgumentException($"{key} must be an integer from {minimum} to {maximum}.");
        return value;
    }
    var iterations = Number("--iterations", 10000, 1, 10000000);
    var warmup = Number("--warmup", 500, 0, 1000000);
    var samples = Number("--samples", 5, 1, 100);
    var seed = Number("--seed", 42, int.MinValue, int.MaxValue);
#if DEBUG
    Console.WriteLine("Debug build: use -c Release for useful performance comparisons.");
#endif
    Console.WriteLine($"Domain benchmark: {iterations} operations/sample; {warmup} warmup; {samples} samples; seed={seed}");
    var report = BenchmarkRunner.Run(scenario, iterations, warmup, samples, seed);
    if (options.TryGetValue("--json", out var path))
    {
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report: {Path.GetFullPath(path)}");
    }
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Help();
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Benchmark failed: {exception.Message}");
    return 1;
}

static void Help() => Console.WriteLine("""
    RollForHonor.Console smoke
    RollForHonor.Console benchmark [--scenario all|attack|effect|sequence]
        [--iterations 10000] [--warmup 500] [--samples 5] [--seed 42] [--json report.json]
    Default: smoke. Exit codes: 0 success, 1 scenario/runtime failure, 2 invalid arguments.
    Benchmark is opt-in, single-threaded, and has no hardware-dependent pass threshold.
    """);
