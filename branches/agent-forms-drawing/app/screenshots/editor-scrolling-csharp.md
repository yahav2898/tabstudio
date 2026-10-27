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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.01 | 11.05 | 99% | 0.37 | 1.72 | 4.12 | 119.8 → 121.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.59 | 12.65 | 99% | 0.60 | 2.01 | 4.43 | 157.9 → 156.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 4.71 | 80.57 | 99% | 2.58 | 2.62 | 5.00 | 195.3 → 178.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.22 | 14.22 | 99% | 0.50 | 1.79 | 4.26 | 137.8 → 134.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.95 | 11.08 | 99% | 0.50 | 1.60 | 4.01 | 114.6 → 107.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.08 | 14.61 | 99% | 0.50 | 1.72 | 4.16 | 139.9 → 132.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 3.48 | 15.73 | 99% | 0.66 | 2.49 | 4.79 | 140.4 → 132.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 2.62 | 12.87 | 99% | 0.59 | 2.16 | 4.69 | 139.5 → 131.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.53 | 11.60 | 99% | 0.55 | 1.98 | 4.52 | 117.4 → 112.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.36 | 12.87 | 99% | 0.59 | 1.95 | 4.62 | 142.6 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 2.90 | 16.91 | 99% | 0.64 | 2.31 | 4.22 | 142.6 → 135.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 2.49 | 12.80 | 100% | 0.56 | 1.82 | 3.69 | 79.2 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.212 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.82 | 2.30 | 2.42 | 100% | 0.47 | 1.85 | 4.98 | 85.1 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 233 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 2.54 | 12.99 | 99% | 0.60 | 2.04 | 4.76 | 138.2 → 135.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.57 | 5.22 | 20.42 | 98% | 0.89 | 4.57 | 5.24 | 352.3 → 345.2 | 9 | 1.1 / 4 |

