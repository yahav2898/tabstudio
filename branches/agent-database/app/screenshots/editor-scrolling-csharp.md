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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.78 | 9.32 | 99% | 0.39 | 2.24 | 3.94 | 115.3 → 117.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 2.92 | 13.48 | 99% | 0.58 | 2.23 | 4.15 | 154.6 → 151.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 2.01 | 12.36 | 100% | 0.55 | 1.63 | 3.60 | 190.3 → 174.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 2.11 | 9.19 | 99% | 0.44 | 1.76 | 3.62 | 133.2 → 130.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.90 | 9.45 | 99% | 0.41 | 1.69 | 3.49 | 109.7 → 102.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 2.88 | 16.40 | 99% | 0.50 | 2.23 | 3.89 | 135.8 → 128.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.62 | 10.51 | 99% | 0.43 | 1.97 | 4.11 | 136.2 → 127.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.52 | 1.98 | 10.62 | 99% | 0.45 | 1.70 | 3.79 | 134.7 → 126.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.47 | 1.89 | 9.20 | 100% | 0.36 | 1.65 | 3.44 | 112.3 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.51 | 1.60 | 10.25 | 99% | 0.42 | 1.30 | 3.66 | 137.9 → 130.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.87 | 13.88 | 99% | 0.51 | 2.40 | 3.47 | 138.1 → 130.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.76 | 73.08 | 99% | 0.42 | 1.38 | 3.19 | 73.9 → 69.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.147 |
| AttachingStyle - | 0.043 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.44 | 1.75 | 12.01 | 98% | 0.36 | 1.32 | 3.90 | 79.8 → 101.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 1.57 | 10.31 | 99% | 0.40 | 1.28 | 3.55 | 133.4 → 130.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.09 | 4.28 | 20.36 | 98% | 2.33 | 3.86 | 4.11 | 347.6 → 341.5 | 8 | 1.1 / 4 |

