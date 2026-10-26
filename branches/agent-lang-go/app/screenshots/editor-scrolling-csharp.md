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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 2.58 | 12.86 | 99% | 0.53 | 2.10 | 4.96 | 113.9 → 115.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 2.96 | 12.20 | 99% | 0.79 | 2.26 | 5.13 | 153.2 → 150.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 3.72 | 15.70 | 99% | 2.04 | 2.71 | 5.11 | 189.6 → 173.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.81 | 11.17 | 99% | 0.71 | 2.18 | 4.97 | 132.2 → 129.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 2.60 | 12.72 | 99% | 0.70 | 2.00 | 4.79 | 108.9 → 101.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 3.47 | 17.14 | 99% | 0.80 | 2.39 | 5.20 | 134.5 → 127.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 2.74 | 12.77 | 99% | 0.71 | 2.16 | 4.97 | 134.4 → 126.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 2.81 | 12.31 | 99% | 0.72 | 2.21 | 4.91 | 133.9 → 125.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.97 | 2.83 | 12.34 | 99% | 0.65 | 2.18 | 4.84 | 110.9 → 106.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 2.92 | 74.23 | 99% | 0.72 | 2.24 | 5.24 | 137.5 → 129.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 2.69 | 14.78 | 99% | 0.68 | 2.07 | 4.55 | 137.2 → 128.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 2.33 | 8.40 | 100% | 0.61 | 1.81 | 4.24 | 72.7 → 69.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.437 |
| AttachingStyle - | 0.090 |
| ArrangingLayoutable CachedLineNumberMargin | 0.043 |
| ArrangingLayoutable TextView | 0.034 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.87 | 2.46 | 2.74 | 100% | 0.50 | 1.93 | 5.21 | 78.5 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 238 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.03 | 2.80 | 12.38 | 99% | 0.69 | 2.17 | 5.13 | 132.9 → 130.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.64 | 5.77 | 21.87 | 98% | 2.51 | 4.62 | 5.32 | 347.0 → 341.4 | 9 | 1.1 / 4 |

