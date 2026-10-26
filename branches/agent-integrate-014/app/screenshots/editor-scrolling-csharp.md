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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.06 | 10.82 | 99% | 0.43 | 1.75 | 3.76 | 113.3 → 113.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 1.93 | 10.96 | 99% | 0.51 | 1.53 | 3.52 | 150.3 → 148.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 2.26 | 14.04 | 99% | 0.69 | 1.76 | 3.81 | 186.5 → 170.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.00 | 11.44 | 99% | 0.48 | 1.60 | 3.53 | 130.1 → 127.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.89 | 15.82 | 100% | 0.58 | 2.36 | 3.70 | 107.3 → 100.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 1.81 | 13.45 | 99% | 0.46 | 1.50 | 3.51 | 132.5 → 124.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 1.85 | 9.92 | 99% | 0.45 | 1.48 | 3.56 | 132.6 → 124.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.52 | 1.68 | 9.95 | 99% | 0.43 | 1.40 | 3.27 | 131.3 → 123.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.87 | 8.39 | 99% | 0.41 | 1.52 | 3.48 | 109.5 → 104.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.78 | 11.66 | 99% | 0.48 | 2.41 | 3.72 | 134.8 → 127.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.91 | 12.39 | 99% | 0.52 | 2.48 | 3.55 | 135.5 → 127.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.53 | 9.07 | 100% | 0.36 | 1.25 | 2.50 | 71.1 → 67.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.43 | 1.67 | 1.81 | 100% | 0.35 | 1.35 | 3.54 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 9 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 1.99 | 12.19 | 99% | 0.51 | 1.57 | 3.74 | 130.9 → 128.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 4.41 | 17.99 | 98% | 2.32 | 3.90 | 4.19 | 343.8 → 336.7 | 8 | 1.1 / 4 |

