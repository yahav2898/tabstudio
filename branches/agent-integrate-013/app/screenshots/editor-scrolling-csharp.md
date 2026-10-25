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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 3.54 | 13.08 | 100% | 0.43 | 2.61 | 3.92 | 113.3 → 114.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.63 | 12.66 | 99% | 0.44 | 1.29 | 3.40 | 151.3 → 149.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 1.70 | 12.33 | 99% | 0.52 | 1.30 | 3.33 | 187.4 → 171.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 1.66 | 10.47 | 99% | 0.42 | 1.33 | 3.46 | 130.8 → 128.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 1.38 | 8.63 | 100% | 0.38 | 1.09 | 3.14 | 107.4 → 100.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 2.78 | 13.69 | 99% | 0.53 | 2.21 | 3.46 | 133.3 → 125.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 2.57 | 12.00 | 99% | 0.45 | 1.39 | 3.52 | 133.4 → 125.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.52 | 1.54 | 10.60 | 99% | 0.43 | 1.23 | 3.38 | 132.4 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.49 | 1.57 | 9.66 | 99% | 0.40 | 1.31 | 3.33 | 109.9 → 104.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.67 | 10.83 | 99% | 0.46 | 1.31 | 3.45 | 135.7 → 128.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 2.37 | 10.13 | 99% | 0.48 | 1.48 | 3.28 | 135.7 → 127.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.37 | 11.12 | 100% | 0.40 | 1.27 | 2.72 | 71.4 → 67.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.193 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.57 | 1.65 | 2.11 | 100% | 0.39 | 1.34 | 3.79 | 77.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 7 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.57 | 10.04 | 100% | 0.39 | 1.24 | 3.35 | 131.3 → 128.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.96 | 3.27 | 18.20 | 98% | 0.55 | 2.70 | 3.64 | 344.6 → 339.0 | 9 | 1.1 / 4 |

