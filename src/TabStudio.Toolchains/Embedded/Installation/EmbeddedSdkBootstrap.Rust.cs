using System.Text.Json;
using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Embedded.Installation;

public static partial class EmbeddedSdkBootstrap
{
    public static partial class Recipes
    {
        private const string RustPrefix = "/opt/tabstudio/embedded-rust";
        private const string RustInstallers = "/opt/tabstudio/embedded-rust-installers";
        private static readonly EmbeddedSdkRecipe Rust = CreateRust();

        private static EmbeddedSdkRecipe CreateRust()
        {
            using var manifest = JsonDocument.Parse(Requirements("rust-components.lock.json"));
            var root = manifest.RootElement;
            var setup = new List<SetupStepDefinition>();
            var targets = new List<string>();
            foreach (var component in root.GetProperty("components").EnumerateArray())
            {
                var url = new Uri(component.GetProperty("url").GetString()!);
                var name = component.GetProperty("name").GetString()!;
                var target = component.GetProperty("target").GetString()!;
                var architecture = component.GetProperty("architecture").GetString();
                var folder = Path.GetFileName(url.AbsolutePath)[..^".tar.xz".Length];
                var destination = RustInstallers + "/" + folder;
                setup.Add(new SetupStepDefinition
                {
                    Name = "Rust 1.97.0 " + name + " (" + target + ")",
                    Architectures = architecture is null ? [] : [architecture],
                    Archive = new ArchiveDefinition
                    {
                        Url = url,
                        Sha256 = component.GetProperty("sha256").GetString()!,
                        Destination = destination,
                    },
                    Command = ["/bin/sh", destination + "/" + folder + "/install.sh", "--prefix=" + RustPrefix, "--disable-ldconfig"],
                });
                if (name == "rust-std" && architecture is null)
                {
                    targets.Add(target);
                }
            }
            var files = new List<FileDefinition>();
            foreach (var command in new[] { "cargo", "rustc", "rust-analyzer", "rustfmt", "cargo-fmt", "cargo-clippy", "clippy-driver" })
            {
                files.Add(new FileDefinition
                {
                    Path = "/usr/local/bin/" + command,
                    Executable = true,
                    Content = "#!/bin/sh\nexport CARGO_HOME=\"${CARGO_HOME:-/root/.cargo}\"\nexport CARGO_BUILD_JOBS=\"${CARGO_BUILD_JOBS:-2}\"\nexport CARGO_NET_GIT_FETCH_WITH_CLI=\"${CARGO_NET_GIT_FETCH_WITH_CLI:-true}\"\nexport PATH=/opt/tabstudio/embedded/build-bin:" + RustPrefix + "/bin:$PATH\nexec " + RustPrefix + "/bin/" + command + " \"$@\"\n",
                });
            }
            files.Add(new FileDefinition
            {
                Path = RustPrefix + "/TABSTUDIO-SDK.txt",
                Content = "Rust 1.97.0 native glibc components, rust-analyzer, rustfmt, Clippy, rust-src and ten standard bare-metal targets are pinned to the official 2026-07-09 distribution manifest and archive SHA-256 values. No rustup install/update script is evaluated. This SDK runs its selected compiler directly; rust-toolchain overrides and cargo +toolchain need a separately installed rustup. AVR/Xtensa and other tier-3 Rust targets need a separate compiler/build-std setup. Crates follow each project's Cargo.lock and Cargo's registry checksums; generated locks remain project-owned.\n",
            });
            return new EmbeddedSdkRecipe("embedded-rust", root.GetProperty("revision").GetString()!,
                new Uri("https://github.com/rust-lang/rust.git"), RustPrefix, ["pkg-config", "libssl-dev"],
                [
                    Command(RustPrefix + "/bin/rustc", ["--version"], "/"),
                    Command(RustPrefix + "/bin/cargo", ["--version"], "/"),
                    Command(RustPrefix + "/bin/rust-analyzer", ["--version"], "/"),
                    Command("python3", ["-c", "import pathlib,sys; root=pathlib.Path(sys.argv[1]); missing=[t for t in sys.argv[2:] if not any((root/'lib/rustlib'/t/'lib').glob('libcore-*.rlib'))]; assert not missing, 'Missing installed Rust targets: '+repr(missing); print('EMBEDDED_RUST_TARGETS_PASSED')", RustPrefix, .. targets], "/"),
                    // The verified compressed archives and installed component notices remain available.
                    // Expanded installers duplicate approximately 1.4 GB and are no longer needed on a tablet.
                    Command("python3", ["-c", "import shutil,sys; shutil.rmtree(sys.argv[1]); print('RUST_EXPANDED_INSTALLERS_REMOVED')", RustInstallers], "/"),
                ], files,
                new Dictionary<string, string?>
                {
                    ["CARGO_HOME"] = "/root/.cargo",
                    ["CARGO_BUILD_JOBS"] = "2",
                    // libgit2 rejects PRoot's transient link2symlink reference names; signed Git handles them.
                    ["CARGO_NET_GIT_FETCH_WITH_CLI"] = "true",
                    ["PATH"] = RustPrefix + "/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                })
            {
                CheckoutSource = false,
                Setup = setup,
                Removes = [RustInstallers],
                RequiredArtifacts = [RustPrefix + "/bin/rustc", RustPrefix + "/bin/cargo", RustPrefix + "/bin/rust-analyzer", RustPrefix + "/lib/rustlib/src/rust/library/core/Cargo.toml"],
            };
        }
    }
}
