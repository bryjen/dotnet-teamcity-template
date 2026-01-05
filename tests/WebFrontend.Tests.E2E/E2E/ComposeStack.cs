using System.Diagnostics;
using System.Net;

namespace WebFrontend.Tests.E2E;

public sealed class ComposeStack
{
    private readonly string _workingDir;

    public ComposeStack(string workingDir)
    {
        _workingDir = workingDir;
    }

    public async Task UpAsync(CancellationToken ct)
    {
        await RunAsync("docker compose up -d --build", ct);
        await WaitForHttpOkAsync("http://localhost:5026/", TimeSpan.FromSeconds(60), ct);
        await WaitForHttpOkAsync("http://localhost:8080/", TimeSpan.FromSeconds(60), ct);
    }

    public Task DownAsync(CancellationToken ct)
        => RunAsync("docker compose down", ct);

    private async Task RunAsync(string command, CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{command}\"",
                WorkingDirectory = _workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        proc.Start();

        var stdOut = await proc.StandardOutput.ReadToEndAsync(ct);
        var stdErr = await proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            throw new Exception($"Command failed: {command}\nExitCode: {proc.ExitCode}\nSTDOUT:\n{stdOut}\nSTDERR:\n{stdErr}");
        }
    }

    private static async Task WaitForHttpOkAsync(string url, TimeSpan timeout, CancellationToken ct)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var resp = await http.GetAsync(url, ct);
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch
            {
                // ignore and retry
            }

            await Task.Delay(500, ct);
        }

        throw new TimeoutException($"Timed out waiting for {url} to return 200 OK.");
    }
}


