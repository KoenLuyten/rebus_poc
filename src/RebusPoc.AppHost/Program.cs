using System.Diagnostics;
using System.Runtime.InteropServices;
using Testcontainers.MsSql;

var solutionRoot = FindSolutionRoot();

Console.WriteLine("[apphost] Starting SQL Server container...");
await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
await container.StartAsync();

var connectionString = container.GetConnectionString();
Console.WriteLine($"[apphost] SQL Server ready: {connectionString}");

using var shutdown = new CancellationTokenSource();
void OnSignal(PosixSignalContext context)
{
    context.Cancel = true;
    shutdown.Cancel();
}
using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal);
using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);

// Start the client first, so its subscription exists before the provider starts publishing
using var client = StartProject("client", "RebusPoc.EventClient");
await Task.Delay(TimeSpan.FromSeconds(5));
using var provider = StartProject("provider", "RebusPoc.EventProvider");

Console.WriteLine("[apphost] Running. Press Ctrl+C to stop.");
try
{
    await Task.WhenAny(
        Task.Delay(Timeout.Infinite, shutdown.Token),
        client.WaitForExitAsync(),
        provider.WaitForExitAsync());
}
catch (OperationCanceledException)
{
}

Console.WriteLine("[apphost] Stopping...");
foreach (var process in new[] { provider, client })
{
    if (!process.HasExited)
    {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }
}

Process StartProject(string name, string project)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        ArgumentList = { "run", "--no-build", "--project", Path.Combine(solutionRoot, "src", project) },
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.Environment["ConnectionStrings__Rebus"] = connectionString;
    startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"[{name}] {e.Data}"); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine($"[{name}] {e.Data}"); };
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static string FindSolutionRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RebusPoc.slnx")))
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? throw new InvalidOperationException("Could not find RebusPoc.slnx");
}
