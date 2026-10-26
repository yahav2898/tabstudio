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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 1.68 | 2.22 | 100% | 0.34 | 1.41 | 4.04 | 119.4 → 120.5 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 1.83 | 51.07 | 100% | 0.52 | 1.41 | 3.92 | 157.2 → 155.0 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 2.05 | 51.88 | 100% | 0.62 | 1.52 | 4.09 | 193.1 → 177.5 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.81 | 4.09 | 100% | 0.50 | 2.39 | 4.01 | 137.1 → 134.5 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.46 | 23.08 | 100% | 0.46 | 2.24 | 3.44 | 113.9 → 106.6 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 1.65 | 2.03 | 100% | 0.45 | 1.28 | 3.64 | 138.8 → 130.8 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.67 | 47.11 | 100% | 0.49 | 1.34 | 3.81 | 139.4 → 131.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 1.66 | 48.17 | 100% | 0.46 | 1.31 | 3.68 | 138.1 → 130.0 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 1.61 | 1.88 | 100% | 0.39 | 1.32 | 3.54 | 116.0 → 110.8 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.98 | 45.06 | 100% | 0.76 | 2.42 | 4.13 | 142.1 → 134.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 1.81 | 3.74 | 100% | 0.46 | 1.43 | 3.47 | 141.5 → 133.4 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.42 | 41.01 | 100% | 0.43 | 1.15 | 2.94 | 77.7 → 74.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.165 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 1.71 | 1.84 | 100% | 0.42 | 1.39 | 4.21 | 83.9 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 31 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 1.80 | 2.40 | 100% | 0.49 | 1.46 | 4.16 | 137.7 → 135.0 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 4.41 | 26.73 | 99% | 2.34 | 3.87 | 5.07 | 350.7 → 344.5 | 1 | 1.1 / 4 |

