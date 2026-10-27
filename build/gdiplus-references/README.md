# GDI+ reference pictures

The Forms layer's drawing tests (`tests/TabStudio.Forms.Tests/Drawing`) compare what TabStudio draws on a bitmap
with what GDI+ draws. This tool draws the same scenes, from `DrawingScenes.cs`, with GDI+ on Windows:

```bash
dotnet run --project build/gdiplus-references -- tests/TabStudio.Forms.Tests/Drawing/References
```

It writes one PNG per scene marked as drawn by GDI+, and `hatch-patterns.txt`, GDI+'s 8 by 8 hatch patterns, which
`src/TabStudio.Forms/Rendering/HatchPatterns.cs` holds. Scenes with text are held to TabStudio's own earlier output
instead, since the fonts on a tablet differ from Windows'.

Run it again after adding or changing a scene, and review the new pictures before committing them.
