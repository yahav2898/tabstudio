using TabStudio.Testing;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.Tests;

/// <summary>
/// A runtime pack laid out like <c>Microsoft.NETCore.App.Runtime.Mono.browser-wasm</c>, with a <c>dotnet.js</c> that
/// has the real pack's placeholders and stand-ins for the other files, for tests that need no download.
/// </summary>
internal static class FakeBrowserRuntime
{
    /// <summary>The placeholders of the .NET 10 <c>dotnet.js</c>, in the shape the real one has them.</summary>
    public const string DotnetJs = """
        //! Licensed to the .NET Foundation under one or more agreements.
        /*! bundlerFriendlyImports */
        var e=!1;export const dotnet = { withConfig(c){ return c; } };
        dotnet.withConfig(/*! dotnetBootConfig */{});export{dotnet as default};
        """;

    public static BrowserRuntime Create(TempDirectory temp, string version = "10.0.13")
    {
        var root = $"packs/microsoft.netcore.app.runtime.mono.browser-wasm/{version}";
        temp.Write($"{root}/runtimes/browser-wasm/native/dotnet.js", DotnetJs);
        temp.Write($"{root}/runtimes/browser-wasm/native/dotnet.native.js", "// native");
        temp.Write($"{root}/runtimes/browser-wasm/native/dotnet.runtime.js", "// runtime");
        temp.Write($"{root}/runtimes/browser-wasm/native/dotnet.native.wasm", "\0asm");
        foreach (var icu in BrowserRuntime.ShardedIcuFiles.Append(BrowserRuntime.FullIcuFile))
        {
            temp.Write($"{root}/runtimes/browser-wasm/native/{icu}", icu);
        }

        temp.Write($"{root}/runtimes/browser-wasm/native/System.Private.CoreLib.dll", "corelib");
        temp.Write($"{root}/runtimes/browser-wasm/lib/net10.0/System.Runtime.InteropServices.JavaScript.dll", "interop");
        temp.Write($"{root}/runtimes/browser-wasm/lib/net10.0/System.Runtime.dll", "runtime");
        temp.Write($"{root}/runtimes/browser-wasm/lib/net10.0/System.Text.Json.dll", "json");
        return new BrowserRuntime(temp.Resolve(root));
    }
}
