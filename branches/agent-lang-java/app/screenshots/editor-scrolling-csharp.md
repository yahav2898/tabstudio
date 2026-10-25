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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.76 | 9.21 | 100% | 0.46 | 1.43 | 4.10 | 109.9 → 111.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 1.96 | 9.96 | 99% | 0.55 | 1.46 | 3.65 | 148.2 → 146.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 4.08 | 14.32 | 99% | 2.57 | 2.68 | 4.44 | 184.6 → 169.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 1.94 | 9.61 | 99% | 0.50 | 1.53 | 3.90 | 128.3 → 125.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 1.81 | 9.98 | 99% | 0.52 | 1.34 | 3.99 | 104.8 → 98.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.79 | 10.68 | 99% | 0.51 | 1.39 | 3.87 | 130.1 → 122.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 1.85 | 9.67 | 99% | 0.45 | 1.45 | 3.72 | 130.1 → 122.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.96 | 13.74 | 99% | 0.55 | 2.42 | 3.97 | 130.2 → 121.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.82 | 7.45 | 100% | 0.45 | 1.43 | 3.66 | 107.2 → 102.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 1.64 | 9.45 | 99% | 0.42 | 1.31 | 3.40 | 132.6 → 124.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.50 | 1.51 | 8.86 | 99% | 0.41 | 1.21 | 2.92 | 132.6 → 124.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.30 | 6.25 | 100% | 0.36 | 1.03 | 2.49 | 68.8 → 65.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.206 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable ScrollingTextLayer | 0.026 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable CachedLineNumberMargin | 0.017 |
| ArrangingLayoutable LineDrawing | 0.017 |
| FindingResource - | 0.010 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| touch drag and flick (real touch input) | 46 | 0.58 | 2.58 | 3.96 | 100% | 0.40 | 2.32 | 3.71 | 75.0 → 96.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 59 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 1.79 | 8.24 | 100% | 0.47 | 1.40 | 3.62 | 128.5 → 125.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.83 | 1.98 | 13.71 | 98% | 0.49 | 1.56 | 3.46 | 340.8 → 333.9 | 7 | 1.1 / 4 |

