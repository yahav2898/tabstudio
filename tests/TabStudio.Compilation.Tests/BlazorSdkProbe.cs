using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// A temporary experiment that runs only on CI: it records what the .NET SDK does for Razor, Blazor and
/// WebAssembly projects, so TabStudio can do the same. It never fails.
/// </summary>
public sealed class BlazorSdkProbe
{
    private const string Script = """
        set -x
        OUT="$1"
        W=/tmp/probe-work
        mkdir -p "$OUT" "$W"
        export NUGET_PACKAGES=/tmp/probe-nuget
        export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
        dotnet --info > "$OUT/dotnet-info-default.txt" 2>&1
        dotnet --list-sdks > "$OUT/sdks-default.txt" 2>&1
        DEFSDK=$(dotnet --list-sdks | tail -n 1 | sed -E 's/^([^ ]+) \[(.*)\]$/\2\/\1/')
        echo "$DEFSDK" > "$OUT/default-sdk-dir.txt"
        ls -la "$DEFSDK/Sdks/Microsoft.NET.Sdk.Razor/source-generators" > "$OUT/default-razor-generators.txt" 2>&1
        mkdir -p "$OUT/razor-default"
        cp "$DEFSDK"/Sdks/Microsoft.NET.Sdk.Razor/source-generators/*.dll "$OUT/razor-default/" 2>/dev/null

        curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
        bash /tmp/dotnet-install.sh --channel 10.0.1xx --install-dir /tmp/dotnet1xx --no-path > "$OUT/install-1xx.log" 2>&1
        D=/tmp/dotnet1xx/dotnet
        export DOTNET_ROOT=/tmp/dotnet1xx
        export PATH=/tmp/dotnet1xx:$PATH
        $D --list-sdks > "$OUT/sdks-1xx.txt" 2>&1
        SDKDIR=$(ls -d /tmp/dotnet1xx/sdk/10.0.1* | tail -n 1)
        echo "$SDKDIR" > "$OUT/sdk-1xx-dir.txt"
        mkdir -p "$OUT/razor-1xx"
        cp -r "$SDKDIR"/Sdks/Microsoft.NET.Sdk.Razor/source-generators/. "$OUT/razor-1xx/"
        ls -la "$SDKDIR"/Sdks/Microsoft.NET.Sdk.Razor/source-generators > "$OUT/razor-1xx-listing.txt"
        ls -la "$SDKDIR"/Roslyn/bincore > "$OUT/roslyn-1xx-listing.txt"
        ls "$SDKDIR"/Sdks > "$OUT/sdks-folders.txt"
        mkdir -p "$OUT/sdk-targets"
        for s in Microsoft.NET.Sdk.Razor Microsoft.NET.Sdk.StaticWebAssets Microsoft.NET.Sdk.BlazorWebAssembly Microsoft.NET.Sdk.WebAssembly Microsoft.NET.Sdk.Web Microsoft.NET.Sdk.Web.ProjectSystem Microsoft.NET.Sdk.Publish; do
          if [ -d "$SDKDIR/Sdks/$s" ]; then
            (cd "$SDKDIR/Sdks/$s" && find . \( -name '*.props' -o -name '*.targets' -o -name '*.json' -o -name '*.razorencconfig' \) -exec install -D {} "$OUT/sdk-targets/$s/{}" \;)
            (cd "$SDKDIR/Sdks/$s" && find . -type f | sort) > "$OUT/sdk-targets/$s.files.txt"
          fi
        done
        cp "$SDKDIR"/Microsoft.NETCoreSdk.BundledVersions.props "$OUT/sdk-targets/"
        cp "$SDKDIR"/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.FrameworkReferenceResolution.targets "$OUT/sdk-targets/"
        cp "$SDKDIR"/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.GenerateGlobalUsings.targets "$OUT/sdk-targets/" 2>/dev/null
        cp "$SDKDIR"/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.BeforeCommon.targets "$OUT/sdk-targets/" 2>/dev/null
        $D new list > "$OUT/templates-list.txt" 2>&1

        mkdir -p "$OUT/templates" "$OUT/builds"
        make() {
          name="$1"; shift
          rm -rf "$W/$name"; mkdir -p "$W/$name"
          (cd "$W/$name" && $D new "$@" > "$OUT/builds/$name.new.log" 2>&1)
          (cd "$W/$name" && find . -type f -not -path '*/bin/*' -not -path '*/obj/*' -exec install -D {} "$OUT/templates/$name/{}" \;)
        }
        build() {
          name="$1"; proj="$2"
          (cd "$W/$name" && $D build "$proj" -p:EmitCompilerGeneratedFiles=true -v:d > "$W/$name.build.log" 2>&1; echo "exit $?" >> "$W/$name.build.log")
          grep -E "error|warning CS|warning RZ|Build succeeded|exit [0-9]" "$W/$name.build.log" | head -n 200 > "$OUT/builds/$name.summary.txt"
          grep -E "csc\.dll|/additionalfile:|/analyzerconfig:" "$W/$name.build.log" | head -n 20 > "$OUT/builds/$name.csc.txt"
          (cd "$W/$name" && find . -type f \( -path '*/obj/*' -o -path '*/bin/*' \) -printf '%s %p\n' | sort -k2) > "$OUT/builds/$name.files.txt"
          (cd "$W/$name" && find . -type f \( -path '*/obj/*' -o -path '*/bin/*' \) \( -name '*.editorconfig' -o -name '*staticwebassets*.json' -o -name '*.runtimeconfig.json' -o -name '*.deps.json' -o -name '*.g.cs' -o -name '*.razor.cs' -o -name '*.cshtml.cs' -o -name '*.rz.scp.css' -o -name '*.styles.css' -o -name '*.bundle.scp.css' -o -name 'dotnet.js' -o -name 'dotnet.boot.js' -o -name 'blazor.boot.json' -o -name '*.html' -o -name '*.cache.json' -o -name '*.razor.g.cs' \) -size -600k -exec install -D {} "$OUT/builds/$name/{}" \;)
          (cd "$W/$name" && for a in $(find . -name project.assets.json); do echo "== $a"; jq -r '.libraries | keys[]' "$a"; done) > "$OUT/builds/$name.packages.txt"
        }
        serve() {
          name="$1"; proj="$2"; port="$3"; shift 3
          (cd "$W/$name" && ASPNETCORE_ENVIRONMENT=Development $D run --no-build --project "$proj" --urls "http://127.0.0.1:$port" > "$W/$name.run.log" 2>&1 &)
          sleep 12
          mkdir -p "$OUT/served/$name"
          for path in "$@"; do
            f=$(echo "$path" | tr '/?' '__')
            curl -sS -D "$OUT/served/$name/$f.headers" "http://127.0.0.1:$port$path" -o "$OUT/served/$name/$f.body" --max-time 20
          done
          pkill -f "urls http://127.0.0.1:$port" || true
          sleep 2
          cp "$W/$name.run.log" "$OUT/served/$name/run.log"
        }

        make blazor-server blazor -n BlazorApp1 -o BlazorApp1
        build blazor-server BlazorApp1/BlazorApp1.csproj
        serve blazor-server BlazorApp1/BlazorApp1.csproj 5301 / /counter /weather /_framework/blazor.web.js /app.css /BlazorApp1.styles.css /lib/bootstrap/dist/css/bootstrap.min.css /favicon.png

        make blazor-wasm blazor -n BlazorApp1 -o BlazorApp1 --interactivity WebAssembly
        build blazor-wasm BlazorApp1/BlazorApp1/BlazorApp1.csproj
        serve blazor-wasm BlazorApp1/BlazorApp1/BlazorApp1.csproj 5302 / /counter /_framework/blazor.web.js /_framework/dotnet.js /_framework/BlazorApp1.Client.wasm /_framework/BlazorApp1.Client.dll

        make blazor-auto blazor -n BlazorApp1 -o BlazorApp1 --interactivity Auto
        build blazor-auto BlazorApp1/BlazorApp1/BlazorApp1.csproj

        make blazor-global blazor -n BlazorApp1 -o BlazorApp1 --all-interactive
        make blazor-none blazor -n BlazorApp1 -o BlazorApp1 --interactivity None
        make blazor-empty blazor -n BlazorApp1 -o BlazorApp1 --empty

        make blazorwasm blazorwasm -n BlazorApp1 -o BlazorApp1
        build blazorwasm BlazorApp1/BlazorApp1.csproj
        (cd "$W/blazorwasm" && $D publish BlazorApp1/BlazorApp1.csproj -c Release -o "$W/blazorwasm-pub" -p:PublishTrimmed=false -p:WasmEnableWebcil=false > "$OUT/builds/blazorwasm.publish.log" 2>&1)
        (cd "$W/blazorwasm-pub" && find . -type f -printf '%s %p\n' | sort -k2) > "$OUT/builds/blazorwasm.publish.files.txt"
        mkdir -p "$OUT/builds/blazorwasm-pub"
        cp "$W/blazorwasm-pub/wwwroot/index.html" "$OUT/builds/blazorwasm-pub/" 2>/dev/null
        cp "$W/blazorwasm-pub/wwwroot/_framework/dotnet.js" "$OUT/builds/blazorwasm-pub/" 2>/dev/null
        cp "$W/blazorwasm-pub/"*.json "$OUT/builds/blazorwasm-pub/" 2>/dev/null

        make webapp webapp -n WebApplication1 -o WebApplication1
        build webapp WebApplication1/WebApplication1.csproj
        serve webapp WebApplication1/WebApplication1.csproj 5303 / /Privacy /css/site.css /lib/jquery/dist/jquery.min.js /WebApplication1.styles.css

        make mvc mvc -n WebApplication1 -o WebApplication1
        build mvc WebApplication1/WebApplication1.csproj
        serve mvc WebApplication1/WebApplication1.csproj 5304 / /Home/Privacy

        make razorclasslib razorclasslib -n RazorClassLibrary1 -o RazorClassLibrary1
        build razorclasslib RazorClassLibrary1/RazorClassLibrary1.csproj

        mkdir -p "$OUT/packages"
        ls "$NUGET_PACKAGES" > "$OUT/packages/all.txt"
        for p in microsoft.aspnetcore.app.internal.assets microsoft.netcore.app.runtime.mono.browser-wasm microsoft.aspnetcore.components.webassembly microsoft.aspnetcore.components.webassembly.devserver microsoft.net.sdk.webassembly.pack microsoft.aspnetcore.components.webassembly.server microsoft.aspnetcore.components.webassembly.authentication microsoft.net.illink.tasks microsoft.aspnetcore.app.runtime.linux-x64 microsoft.aspnetcore.app.ref; do
          if [ -d "$NUGET_PACKAGES/$p" ]; then
            (cd "$NUGET_PACKAGES/$p" && find . -type f -printf '%s %p\n' | sort -k2) > "$OUT/packages/$p.files.txt"
            (cd "$NUGET_PACKAGES/$p" && find . \( -name '*.props' -o -name '*.targets' -o -name '*.nuspec' -o -name '*.json' \) -size -300k -exec install -D {} "$OUT/packages/$p/{}" \;)
          fi
        done
        echo done > "$OUT/DONE"
        """;

    [Fact]
    public void Records_what_the_SDK_does_for_Razor_and_Blazor()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") is not { } workspace)
        {
            return;
        }

        var output = Path.Combine(workspace, "ci-out", "blazor-probe");
        Directory.CreateDirectory(output);
        var script = Path.Combine(Path.GetTempPath(), "blazor-probe.sh");
        File.WriteAllText(script, Script);
        try
        {
            using var process = Process.Start(new ProcessStartInfo("bash", [script, output])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit(TimeSpan.FromMinutes(40));
            File.WriteAllText(Path.Combine(output, "probe-stdout.txt"), stdout.Result);
            File.WriteAllText(Path.Combine(output, "probe-stderr.txt"), stderr.Result);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "probe-exception.txt"), ex.ToString());
        }

        foreach (var folder in new[] { "razor-1xx", "razor-default" })
        {
            var directory = Path.Combine(output, folder);
            if (Directory.Exists(directory))
            {
                File.WriteAllText(Path.Combine(output, folder + "-references.txt"), DescribeAssemblies(directory));
            }
        }
    }

    private static string DescribeAssemblies(string directory)
    {
        var text = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var pe = new PEReader(stream);
                var metadata = pe.GetMetadataReader();
                var definition = metadata.GetAssemblyDefinition();
                text.AppendLine($"{Path.GetRelativePath(directory, file)}: {metadata.GetString(definition.Name)} {definition.Version}");
                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetAssemblyReference(handle);
                    text.AppendLine($"    -> {metadata.GetString(reference.Name)} {reference.Version}");
                }

                foreach (var attributeHandle in definition.GetCustomAttributes())
                {
                    var attribute = metadata.GetCustomAttribute(attributeHandle);
                    if (attribute.Constructor.Kind == HandleKind.MemberReference)
                    {
                        var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                        if (member.Parent.Kind == HandleKind.TypeReference)
                        {
                            var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                            var name = metadata.GetString(type.Name);
                            if (name is "AssemblyInformationalVersionAttribute" or "TargetFrameworkAttribute" or "AssemblyFileVersionAttribute")
                            {
                                var value = metadata.GetBlobReader(attribute.Value);
                                value.ReadUInt16();
                                text.AppendLine($"    [{name}] {value.ReadSerializedString()}");
                            }
                        }
                    }
                }
            }
            catch (BadImageFormatException ex)
            {
                text.AppendLine($"{file}: {ex.Message}");
            }
        }

        return text.ToString();
    }
}
