# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.48 | 10.61 | 100% | 0.49 | 1.86 | 3.66 | 112.6 → 113.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 2.32 | 14.36 | 99% | 0.63 | 1.72 | 3.72 | 149.8 → 147.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 2.38 | 12.75 | 99% | 0.69 | 1.77 | 3.45 | 185.1 → 169.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.24 | 12.15 | 99% | 0.56 | 1.67 | 3.47 | 129.7 → 127.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.08 | 10.38 | 100% | 0.59 | 1.59 | 3.42 | 106.3 → 99.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 4.11 | 30.82 | 99% | 1.15 | 2.74 | 4.24 | 132.5 → 124.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.26 | 12.15 | 99% | 0.58 | 1.72 | 3.47 | 131.9 → 123.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.93 | 2.42 | 14.77 | 99% | 0.61 | 1.84 | 3.56 | 131.5 → 123.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 2.06 | 9.24 | 100% | 0.51 | 1.62 | 3.04 | 109.0 → 104.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 2.30 | 11.30 | 99% | 0.58 | 1.78 | 3.52 | 134.4 → 126.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 2.21 | 12.75 | 99% | 0.54 | 1.68 | 3.12 | 134.0 → 126.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 2.79 | 10.47 | 100% | 0.67 | 2.29 | 2.84 | 71.7 → 68.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.265 |
| ArrangingLayoutable ScrollViewer | 0.037 |
| AttachingStyle - | 0.033 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 2.27 | 2.44 | 100% | 0.50 | 1.81 | 3.77 | 78.1 → 98.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 9 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 2.38 | 14.25 | 99% | 0.59 | 1.81 | 3.70 | 130.5 → 127.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.17 | 2.56 | 16.40 | 98% | 0.63 | 1.97 | 3.54 | 341.6 → 335.2 | 7 | 1.1 / 4 |

