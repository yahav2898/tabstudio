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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.76 | 11.28 | 99% | 0.38 | 2.37 | 4.46 | 112.8 → 114.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.62 | 75.25 | 99% | 0.67 | 2.76 | 4.72 | 151.2 → 148.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.76 | 13.77 | 99% | 0.68 | 2.32 | 4.37 | 185.5 → 169.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.76 | 12.79 | 99% | 0.48 | 2.38 | 4.29 | 130.3 → 127.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.80 | 11.54 | 99% | 0.56 | 2.40 | 4.48 | 107.1 → 100.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 2.73 | 14.16 | 99% | 0.50 | 2.38 | 4.33 | 132.4 → 124.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.81 | 11.62 | 99% | 0.50 | 2.41 | 4.31 | 132.4 → 124.7 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.76 | 12.74 | 99% | 0.50 | 2.38 | 4.15 | 132.0 → 124.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 3.19 | 13.49 | 99% | 0.55 | 2.59 | 4.53 | 110.5 → 105.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.68 | 12.73 | 99% | 0.49 | 2.34 | 4.37 | 135.0 → 127.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.24 | 11.82 | 99% | 0.52 | 1.83 | 3.83 | 135.1 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.21 | 8.99 | 99% | 0.41 | 2.02 | 3.16 | 71.8 → 68.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.264 |
| ArrangingLayoutable ScrollingTextLayer | 0.057 |
| AttachingStyle - | 0.034 |
| ArrangingLayoutable CachedLineNumberMargin | 0.031 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable TextView | 0.018 |
| RaisingRoutedEvent ScrollBar | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 2.59 | 4.69 | 100% | 0.39 | 2.34 | 4.44 | 78.1 → 98.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 117 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 2.15 | 11.31 | 99% | 0.48 | 1.79 | 4.20 | 130.7 → 128.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.24 | 5.23 | 19.13 | 98% | 0.68 | 4.46 | 4.82 | 341.9 → 337.1 | 8 | 1.1 / 4 |

