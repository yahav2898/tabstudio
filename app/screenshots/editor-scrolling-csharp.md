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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.37 | 12.01 | 99% | 0.43 | 1.99 | 3.19 | 112.7 → 113.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 2.43 | 11.67 | 99% | 0.53 | 1.94 | 3.18 | 150.4 → 148.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 2.91 | 21.87 | 98% | 0.67 | 2.22 | 3.40 | 186.3 → 170.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 3.99 | 15.96 | 99% | 0.60 | 2.56 | 3.59 | 130.7 → 128.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.29 | 10.89 | 99% | 0.50 | 1.89 | 3.18 | 107.5 → 100.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 2.38 | 12.45 | 99% | 0.56 | 1.93 | 3.26 | 132.5 → 124.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.68 | 2.35 | 11.91 | 99% | 0.49 | 2.00 | 3.19 | 132.3 → 124.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.53 | 11.78 | 99% | 0.55 | 2.08 | 3.29 | 131.9 → 123.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.48 | 10.64 | 99% | 0.56 | 1.74 | 3.23 | 109.6 → 104.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.03 | 15.89 | 99% | 0.59 | 2.58 | 3.66 | 135.4 → 127.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.06 | 11.96 | 99% | 0.54 | 1.65 | 2.90 | 134.7 → 126.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 1.75 | 14.40 | 99% | 0.47 | 1.37 | 2.32 | 71.3 → 67.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
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
| touch drag and flick (real touch input) | 46 | 0.54 | 1.88 | 13.23 | 98% | 0.42 | 1.43 | 3.20 | 76.3 → 98.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 2.59 | 12.39 | 99% | 0.51 | 2.00 | 3.37 | 130.9 → 128.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.14 | 3.70 | 19.89 | 98% | 0.65 | 2.84 | 3.78 | 344.1 → 336.5 | 8 | 1.1 / 4 |

