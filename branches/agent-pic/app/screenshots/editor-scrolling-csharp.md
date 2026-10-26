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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.60 | 83.03 | 99% | 0.41 | 2.09 | 4.46 | 113.4 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.24 | 11.86 | 99% | 0.55 | 1.80 | 4.22 | 151.0 → 149.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 2.31 | 14.46 | 99% | 0.67 | 1.78 | 4.32 | 186.8 → 170.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.28 | 11.19 | 99% | 0.51 | 1.80 | 4.31 | 131.0 → 128.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.83 | 12.68 | 99% | 0.60 | 2.04 | 4.51 | 108.1 → 101.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.03 | 14.86 | 99% | 0.49 | 1.66 | 4.07 | 133.2 → 125.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.68 | 2.14 | 11.10 | 99% | 0.50 | 1.76 | 4.22 | 133.1 → 125.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 2.19 | 10.96 | 99% | 0.52 | 1.79 | 4.10 | 132.4 → 124.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.95 | 13.99 | 99% | 0.50 | 2.53 | 4.41 | 110.5 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.06 | 10.70 | 99% | 0.49 | 1.70 | 4.16 | 135.7 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.05 | 12.13 | 100% | 0.50 | 1.69 | 3.69 | 135.7 → 127.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.70 | 8.67 | 100% | 0.40 | 1.43 | 3.04 | 71.7 → 68.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.244 |
| RaisingRoutedEvent ScrollBar | 0.043 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 2.78 | 2.89 | 100% | 0.47 | 2.34 | 4.60 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.09 | 11.45 | 99% | 0.52 | 1.69 | 4.26 | 131.6 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.51 | 16.85 | 98% | 0.58 | 2.07 | 4.25 | 343.4 → 337.2 | 8 | 1.1 / 4 |

