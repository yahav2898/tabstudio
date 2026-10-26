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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.93 | 10.38 | 99% | 0.40 | 1.62 | 3.76 | 112.5 → 113.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.23 | 13.44 | 99% | 0.56 | 1.75 | 3.89 | 151.0 → 148.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 2.25 | 13.39 | 99% | 0.72 | 1.69 | 3.87 | 186.6 → 170.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.88 | 8.61 | 99% | 0.45 | 1.52 | 3.53 | 130.1 → 127.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.64 | 12.86 | 99% | 0.50 | 1.85 | 3.69 | 107.4 → 100.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.12 | 11.69 | 99% | 0.53 | 1.66 | 3.77 | 132.9 → 124.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.09 | 12.29 | 99% | 0.52 | 1.69 | 3.86 | 133.0 → 124.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 1.95 | 10.01 | 99% | 0.48 | 1.58 | 3.63 | 132.1 → 123.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 1.89 | 7.49 | 100% | 0.37 | 1.55 | 3.42 | 109.5 → 104.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.99 | 15.70 | 99% | 0.57 | 2.43 | 3.79 | 135.7 → 128.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 3.06 | 15.25 | 99% | 0.57 | 2.52 | 3.70 | 135.7 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.25 | 7.40 | 100% | 0.45 | 1.99 | 2.86 | 71.8 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.198 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.48 | 1.96 | 2.11 | 100% | 0.33 | 1.54 | 3.60 | 77.3 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 8 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.07 | 72.46 | 99% | 0.57 | 2.57 | 3.96 | 131.6 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.89 | 14.55 | 98% | 0.61 | 2.45 | 3.92 | 343.4 → 336.9 | 7 | 1.1 / 4 |

