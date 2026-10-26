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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.06 | 10.31 | 99% | 0.34 | 1.74 | 4.08 | 113.3 → 114.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 2.27 | 10.52 | 99% | 0.56 | 1.78 | 4.19 | 151.1 → 148.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.34 | 13.05 | 99% | 0.68 | 1.81 | 4.34 | 186.7 → 171.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.36 | 14.66 | 99% | 0.65 | 2.61 | 4.59 | 131.1 → 128.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.91 | 12.53 | 100% | 0.47 | 1.56 | 3.95 | 107.7 → 100.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.61 | 2.03 | 14.29 | 99% | 0.50 | 1.67 | 4.10 | 133.2 → 125.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.11 | 11.11 | 99% | 0.47 | 1.74 | 4.20 | 133.1 → 125.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 3.15 | 14.78 | 99% | 0.64 | 2.61 | 4.46 | 132.6 → 124.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.23 | 8.89 | 100% | 0.44 | 1.83 | 4.13 | 110.3 → 105.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.16 | 10.83 | 99% | 0.51 | 1.79 | 4.26 | 135.6 → 128.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 2.06 | 9.93 | 99% | 0.49 | 1.70 | 3.60 | 135.3 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.92 | 8.00 | 100% | 0.43 | 1.56 | 3.11 | 71.7 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.604 |
| AttachingStyle - | 0.085 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 1.73 | 2.04 | 100% | 0.40 | 1.54 | 4.25 | 78.2 → 98.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 17 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.09 | 14.27 | 99% | 0.47 | 1.73 | 4.13 | 131.5 → 128.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 4.82 | 14.21 | 98% | 2.41 | 4.30 | 4.90 | 343.7 → 338.1 | 8 | 1.1 / 4 |

