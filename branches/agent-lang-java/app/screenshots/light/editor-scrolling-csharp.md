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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.76 | 1.93 | 9.63 | 99% | 0.45 | 1.53 | 3.15 | 112.6 → 113.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 1.94 | 13.81 | 99% | 0.54 | 1.47 | 3.09 | 150.3 → 148.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.09 | 13.12 | 99% | 0.63 | 1.56 | 3.15 | 186.2 → 170.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 1.79 | 10.55 | 99% | 0.46 | 1.40 | 3.01 | 130.1 → 127.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.84 | 10.20 | 99% | 0.84 | 2.30 | 3.04 | 106.6 → 100.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 1.78 | 12.19 | 99% | 0.46 | 1.43 | 3.00 | 132.3 → 124.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 1.76 | 9.70 | 99% | 0.46 | 1.36 | 2.98 | 132.1 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 1.98 | 10.77 | 99% | 0.49 | 1.50 | 3.14 | 131.6 → 123.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 1.78 | 9.87 | 99% | 0.43 | 1.41 | 2.92 | 109.5 → 104.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 1.97 | 11.10 | 99% | 0.52 | 1.46 | 3.00 | 134.8 → 127.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 3.73 | 14.81 | 99% | 0.70 | 2.59 | 3.02 | 135.2 → 127.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.54 | 6.62 | 100% | 0.43 | 1.20 | 2.43 | 70.4 → 67.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 1.68 | 1.87 | 100% | 0.40 | 1.29 | 3.09 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 11 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.09 | 10.58 | 99% | 0.52 | 1.60 | 3.38 | 130.7 → 128.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.35 | 4.63 | 18.45 | 97% | 2.57 | 3.96 | 3.77 | 343.8 → 337.5 | 9 | 1.1 / 4 |

