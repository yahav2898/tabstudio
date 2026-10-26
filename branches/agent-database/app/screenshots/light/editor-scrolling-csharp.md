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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.43 | 8.30 | 100% | 0.27 | 1.19 | 3.18 | 114.4 → 115.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.05 | 11.71 | 99% | 0.58 | 2.51 | 3.66 | 153.3 → 150.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.59 | 1.68 | 13.82 | 99% | 0.51 | 1.25 | 3.25 | 188.2 → 172.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 1.45 | 10.02 | 99% | 0.38 | 1.17 | 3.15 | 132.3 → 129.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 1.37 | 8.81 | 99% | 0.37 | 1.09 | 3.02 | 108.8 → 101.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.44 | 1.44 | 12.78 | 99% | 0.39 | 1.17 | 3.13 | 134.4 → 126.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.89 | 12.70 | 99% | 0.56 | 2.31 | 3.52 | 134.6 → 127.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.68 | 66.55 | 99% | 0.39 | 1.31 | 3.36 | 134.1 → 126.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 1.42 | 9.42 | 100% | 0.33 | 1.17 | 3.07 | 111.6 → 106.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.48 | 1.48 | 9.59 | 100% | 0.39 | 1.21 | 3.21 | 137.0 → 129.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 1.46 | 9.77 | 99% | 0.38 | 1.19 | 2.88 | 137.1 → 129.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.28 | 7.96 | 100% | 0.33 | 1.07 | 2.54 | 72.6 → 69.3 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| AttachingStyle - | 0.052 |
| FindingResource - | 0.021 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| ArrangingLayoutable TextView | 0.013 |
| RaisingRoutedEvent Border | 0.010 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 3.50 | 4.18 | 100% | 0.42 | 3.29 | 3.78 | 79.5 → 102.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.52 | 9.55 | 100% | 0.39 | 1.22 | 3.25 | 132.8 → 129.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.77 | 1.83 | 14.30 | 98% | 0.47 | 1.45 | 3.25 | 345.0 → 338.6 | 8 | 1.1 / 4 |

