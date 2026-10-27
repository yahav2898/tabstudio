# rid-assets progress notes (not for the final work)

Task: items 7 (RID-specific + native package assets) and 8 (honest SQL Server message). See agent prompt.

## Design (item 7)
- Packages: RuntimeGraph (fallback table from SDK PortableRuntimeIdentifierGraph subset + merged runtime.json of packages),
  DeviceRuntime identifier (android-{arch} on Android, else RuntimeInformation.RuntimeIdentifier).
- AssetSelector: runtimes/{rid}/lib/{tfm} groups (TFM nearest first, then nearest RID) replace lib/ group; runtimes/{rid}/native nearest RID.
- RestoreResult.NativeLibraries + RuntimeFiles (assemblies + natives). GetPackageRuntimeAssemblies returns RuntimeFiles.
- Compilation: PackageNativeLibraries (IsNativeLibrary by path runtimes/x/native/, resolver: already-mapped (/proc/self/maps) -> Android app copy -> package copy).
  ProgramLoadContext/TestLoadContext/PackageLoadContext override LoadUnmanagedDll and skip natives as assemblies.

## Status
- [ ] item 7 code
