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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.55 | 10.28 | 99% | 0.32 | 1.29 | 3.36 | 112.9 → 114.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 1.67 | 11.17 | 99% | 0.45 | 1.31 | 3.38 | 151.3 → 149.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 1.82 | 13.40 | 99% | 0.58 | 1.34 | 3.44 | 187.5 → 171.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 1.86 | 10.89 | 99% | 0.46 | 1.40 | 3.49 | 130.8 → 128.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 3.59 | 12.66 | 100% | 0.55 | 2.26 | 3.41 | 107.6 → 100.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 1.57 | 10.17 | 99% | 0.42 | 1.22 | 3.29 | 133.3 → 125.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.54 | 13.57 | 99% | 0.44 | 1.24 | 3.32 | 133.2 → 124.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.56 | 10.76 | 99% | 0.43 | 1.27 | 3.27 | 132.3 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.49 | 10.36 | 99% | 0.36 | 1.22 | 3.26 | 109.8 → 104.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.57 | 11.60 | 99% | 0.48 | 1.56 | 3.61 | 135.6 → 128.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.57 | 2.62 | 8.57 | 100% | 0.51 | 1.58 | 3.20 | 136.5 → 127.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.28 | 6.58 | 100% | 0.35 | 1.04 | 2.48 | 71.0 → 67.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.148 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.75 | 1.78 | 100% | 0.38 | 1.35 | 3.73 | 77.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 5 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.55 | 11.21 | 99% | 0.44 | 1.23 | 3.36 | 131.4 → 128.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 4.09 | 74.59 | 98% | 2.37 | 3.71 | 3.73 | 345.6 → 338.6 | 7 | 1.1 / 4 |

