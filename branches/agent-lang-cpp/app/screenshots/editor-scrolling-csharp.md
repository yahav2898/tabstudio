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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 2.83 | 12.22 | 99% | 0.57 | 2.26 | 5.22 | 111.3 → 112.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.26 | 4.04 | 15.42 | 99% | 0.86 | 2.66 | 5.31 | 149.9 → 147.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 3.12 | 15.91 | 99% | 0.92 | 2.28 | 5.27 | 185.8 → 169.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.77 | 13.39 | 99% | 0.69 | 2.12 | 5.03 | 128.9 → 126.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 3.99 | 12.75 | 99% | 0.77 | 2.15 | 5.13 | 105.8 → 99.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.74 | 14.45 | 99% | 0.70 | 2.11 | 5.06 | 131.4 → 123.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 2.57 | 12.01 | 99% | 0.66 | 2.01 | 4.89 | 131.2 → 122.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.00 | 3.04 | 13.97 | 99% | 0.79 | 2.20 | 5.00 | 130.4 → 123.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.97 | 2.70 | 10.41 | 99% | 0.60 | 2.07 | 4.81 | 108.6 → 103.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.05 | 2.76 | 12.73 | 99% | 0.71 | 2.10 | 5.15 | 134.3 → 126.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 2.85 | 12.87 | 99% | 0.70 | 2.15 | 4.66 | 134.0 → 126.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 2.46 | 6.94 | 100% | 0.64 | 1.81 | 4.06 | 70.7 → 67.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 2.64 | 2.79 | 100% | 0.59 | 1.99 | 5.29 | 75.9 → 97.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 235 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 3.55 | 12.56 | 99% | 0.80 | 2.60 | 5.27 | 129.9 → 127.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 3.33 | 19.05 | 98% | 0.86 | 2.51 | 5.12 | 342.8 → 336.4 | 8 | 1.1 / 4 |

