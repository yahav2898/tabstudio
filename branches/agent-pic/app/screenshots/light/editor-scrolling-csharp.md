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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 1.94 | 10.57 | 100% | 0.35 | 1.65 | 4.11 | 112.9 → 113.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.18 | 10.60 | 99% | 0.57 | 1.78 | 4.20 | 150.3 → 148.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 2.32 | 13.82 | 99% | 0.70 | 1.81 | 4.32 | 185.4 → 169.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.78 | 14.34 | 99% | 0.59 | 2.18 | 4.51 | 130.2 → 128.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.53 | 9.32 | 99% | 0.51 | 1.79 | 4.28 | 107.4 → 100.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 2.19 | 14.60 | 99% | 0.52 | 1.78 | 4.27 | 132.4 → 124.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.06 | 11.04 | 99% | 0.51 | 1.69 | 4.19 | 132.5 → 124.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.56 | 13.21 | 99% | 0.59 | 1.93 | 4.48 | 131.7 → 124.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 2.52 | 14.60 | 99% | 0.51 | 1.98 | 4.47 | 110.5 → 105.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.19 | 14.33 | 99% | 0.50 | 1.77 | 4.28 | 135.0 → 127.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.16 | 11.17 | 99% | 0.50 | 1.72 | 3.74 | 134.9 → 127.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.74 | 6.53 | 100% | 0.43 | 1.46 | 3.12 | 71.3 → 67.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.79 | 4.46 | 14.62 | 98% | 2.44 | 2.32 | 5.46 | 79.5 → 99.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 80 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.26 | 15.10 | 99% | 0.56 | 1.80 | 4.42 | 130.8 → 128.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 2.68 | 15.96 | 98% | 0.62 | 2.15 | 4.39 | 341.7 → 335.3 | 9 | 1.1 / 4 |

