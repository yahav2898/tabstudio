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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.78 | 11.05 | 99% | 0.39 | 1.48 | 3.67 | 113.0 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 1.89 | 11.68 | 99% | 0.48 | 1.47 | 3.61 | 150.8 → 148.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 1.86 | 14.23 | 99% | 0.59 | 1.38 | 3.57 | 186.3 → 170.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 3.70 | 12.35 | 99% | 2.31 | 2.46 | 3.85 | 130.6 → 128.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.50 | 9.42 | 99% | 0.41 | 1.19 | 3.23 | 106.5 → 99.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 1.63 | 13.57 | 99% | 0.43 | 1.28 | 3.38 | 132.4 → 124.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 1.70 | 11.77 | 99% | 0.44 | 1.36 | 3.48 | 132.4 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 1.72 | 11.21 | 99% | 0.45 | 1.36 | 3.47 | 131.8 → 123.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.65 | 12.01 | 99% | 0.40 | 1.38 | 3.28 | 109.6 → 104.8 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 3.13 | 12.71 | 99% | 0.50 | 2.39 | 3.66 | 135.9 → 127.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 1.52 | 14.16 | 99% | 0.42 | 1.24 | 2.95 | 134.8 → 127.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.27 | 6.82 | 100% | 0.34 | 1.03 | 2.54 | 70.6 → 67.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.149 |
| AttachingStyle - | 0.043 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
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
| touch drag and flick (real touch input) | 46 | 0.45 | 1.53 | 1.74 | 100% | 0.36 | 1.25 | 3.65 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 3.03 | 75.21 | 99% | 0.67 | 2.36 | 3.76 | 131.0 → 128.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.83 | 1.97 | 16.11 | 98% | 0.48 | 1.54 | 3.48 | 343.3 → 336.5 | 7 | 1.1 / 4 |

