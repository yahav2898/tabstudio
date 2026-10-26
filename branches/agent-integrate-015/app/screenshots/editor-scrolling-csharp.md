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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.09 | 3.50 | 13.41 | 99% | 0.60 | 2.99 | 5.21 | 120.4 → 121.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.38 | 3.89 | 17.97 | 99% | 0.86 | 3.10 | 5.45 | 158.8 → 156.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.39 | 3.83 | 16.72 | 99% | 0.95 | 3.00 | 5.30 | 194.9 → 178.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.47 | 14.98 | 99% | 0.71 | 2.85 | 5.19 | 138.2 → 135.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.98 | 3.31 | 12.64 | 99% | 0.72 | 2.73 | 4.96 | 114.2 → 106.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.17 | 5.11 | 17.58 | 99% | 0.86 | 3.33 | 5.51 | 140.8 → 132.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.10 | 3.56 | 18.37 | 99% | 0.75 | 2.96 | 5.21 | 140.5 → 132.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 3.55 | 14.52 | 99% | 0.79 | 2.94 | 5.13 | 139.7 → 131.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.38 | 15.90 | 99% | 0.65 | 2.85 | 4.92 | 116.9 → 111.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.27 | 3.83 | 16.74 | 99% | 0.80 | 3.09 | 5.35 | 143.4 → 135.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.12 | 3.58 | 17.58 | 99% | 0.73 | 2.91 | 4.74 | 142.3 → 134.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.76 | 3.13 | 12.45 | 99% | 0.64 | 2.58 | 3.89 | 77.3 → 73.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.316 |
| ArrangingLayoutable LineDrawing | 0.059 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.02 | 3.18 | 4.02 | 100% | 0.61 | 2.70 | 5.31 | 84.3 → 107.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 196 frames at 120 Hz; the text stood still on 0 of them, 229 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.21 | 3.79 | 16.76 | 99% | 0.77 | 3.09 | 5.34 | 139.3 → 135.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.66 | 4.21 | 19.86 | 98% | 0.85 | 3.43 | 5.27 | 352.3 → 346.0 | 8 | 1.1 / 4 |

