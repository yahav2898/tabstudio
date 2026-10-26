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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.13 | 10.98 | 99% | 0.46 | 1.70 | 4.13 | 113.4 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.20 | 12.72 | 99% | 0.55 | 1.67 | 3.93 | 152.1 → 149.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.07 | 15.65 | 99% | 2.16 | 1.83 | 4.10 | 188.2 → 172.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.91 | 86.35 | 99% | 0.54 | 1.96 | 3.76 | 131.5 → 128.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.68 | 9.57 | 100% | 0.47 | 1.34 | 3.66 | 107.3 → 100.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.79 | 12.65 | 99% | 0.48 | 1.41 | 3.69 | 133.5 → 125.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.86 | 11.63 | 99% | 0.51 | 1.46 | 3.89 | 133.2 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.24 | 12.65 | 99% | 0.51 | 1.76 | 4.35 | 133.1 → 124.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 3.36 | 11.66 | 99% | 0.73 | 2.40 | 4.15 | 110.9 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.21 | 11.63 | 99% | 0.59 | 1.74 | 4.16 | 136.6 → 128.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 1.97 | 11.72 | 99% | 0.50 | 1.51 | 3.51 | 136.2 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 1.83 | 9.03 | 100% | 0.48 | 1.40 | 3.20 | 71.5 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.268 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 2.13 | 2.22 | 100% | 0.47 | 1.61 | 4.40 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 264 frames at 120 Hz; the text stood still on 0 of them, 54 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.16 | 12.42 | 99% | 0.59 | 1.63 | 4.04 | 132.4 → 129.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 2.32 | 16.75 | 98% | 0.61 | 1.79 | 3.95 | 345.6 → 338.9 | 7 | 1.1 / 4 |

