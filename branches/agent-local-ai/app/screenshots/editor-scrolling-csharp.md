# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 3.91 | 11.21 | 99% | 0.46 | 2.49 | 4.60 | 114.1 → 115.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.26 | 11.84 | 99% | 0.60 | 1.78 | 4.32 | 152.3 → 150.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.28 | 15.52 | 99% | 0.69 | 1.72 | 4.26 | 188.4 → 172.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.61 | 10.47 | 99% | 0.57 | 1.97 | 4.48 | 131.7 → 129.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.26 | 10.51 | 99% | 0.56 | 1.70 | 4.27 | 108.6 → 101.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.24 | 11.27 | 99% | 0.57 | 1.79 | 4.35 | 134.1 → 126.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.92 | 12.61 | 99% | 0.58 | 2.45 | 4.38 | 134.5 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.54 | 12.19 | 99% | 0.80 | 2.72 | 4.57 | 133.8 → 126.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.39 | 9.79 | 99% | 0.47 | 2.16 | 4.14 | 111.1 → 106.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.81 | 12.22 | 99% | 0.57 | 2.35 | 4.43 | 136.8 → 129.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.98 | 12.02 | 99% | 0.57 | 2.47 | 4.03 | 136.8 → 129.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.52 | 7.44 | 100% | 0.50 | 2.15 | 3.40 | 72.9 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.199 |
| AttachingStyle - | 0.050 |
| ArrangingLayoutable CachedLineNumberMargin | 0.050 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 2.21 | 2.86 | 100% | 0.39 | 1.91 | 4.48 | 83.8 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 235 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.10 | 9.91 | 99% | 0.61 | 2.52 | 4.51 | 132.4 → 130.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.23 | 3.43 | 17.96 | 98% | 0.65 | 2.85 | 4.59 | 346.4 → 339.6 | 7 | 1.1 / 4 |

