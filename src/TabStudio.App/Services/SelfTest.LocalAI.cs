using System.Diagnostics;
using System.Security.Cryptography;
using TabStudio.Agent.Local;

namespace TabStudio.App.Services;

/// <summary>Real GGUF inference in the Android app, using a diagnostic model bundled only in CI self-test builds.</summary>
public static partial class SelfTest
{
    private static async Task<SelfTestResult> CheckLocalAIAsync(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await using var resource = typeof(SelfTest).Assembly.GetManifestResourceStream("LocalAI/tiny.gguf");
        if (resource is null)
        {
#if TABSTUDIO_LOCAL_AI_SELF_TEST
            return new SelfTestResult(false, "Local AI: diagnostic GGUF missing from the self-test build", clock.Elapsed);
#else
            return new SelfTestResult(true, "Local AI: diagnostic model not bundled in this build", clock.Elapsed);
#endif
        }

        var path = Path.Combine(Path.GetTempPath(), "tabstudio-ai-selftest-" + Guid.NewGuid().ToString("N") + ".gguf");
        try
        {
            await using (var output = File.Create(path)) await resource.CopyToAsync(output, cancellationToken);
            await using (var file = File.OpenRead(path))
            {
                var hash = await SHA256.HashDataAsync(file, cancellationToken);
                if (!string.Equals(Convert.ToHexString(hash), "047bf46455a544931cff6fef14d7910154c56afbc23ab1c5e56a72e69912c04b", StringComparison.OrdinalIgnoreCase))
                    return new SelfTestResult(false, "Local AI: diagnostic model hash mismatch", clock.Elapsed);
            }
            using var generator = await Task.Run(() => new NativeTextGenerator(path, context: 512), cancellationToken);
            var result = await generator.GenerateAsync("Once upon a time", 16, cancellationToken: cancellationToken);
            return new SelfTestResult(result.Tokens > 0 && result.Text.Length > 0, FormattableString.Invariant($"Local AI: tiny GGUF generated {result.Tokens} tokens at {result.TokensPerSecond:0.0} tokens/s on device"), clock.Elapsed);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new SelfTestResult(false, "Local AI: " + ex.Message, clock.Elapsed);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
