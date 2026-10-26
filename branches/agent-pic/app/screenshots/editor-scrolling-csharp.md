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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 1.96 | 16.55 | 100% | 0.39 | 1.65 | 4.77 | 113.0 → 114.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.88 | 18.00 | 99% | 0.66 | 2.46 | 5.09 | 151.5 → 149.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 2.57 | 18.35 | 99% | 0.72 | 1.97 | 5.12 | 187.0 → 171.4 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.22 | 17.76 | 99% | 0.55 | 1.81 | 4.96 | 130.8 → 128.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.25 | 16.78 | 99% | 0.58 | 1.75 | 4.80 | 107.8 → 101.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.63 | 64.76 | 99% | 0.57 | 1.90 | 5.07 | 133.8 → 125.3 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.07 | 18.04 | 99% | 0.50 | 1.65 | 4.71 | 133.1 → 125.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 2.17 | 17.69 | 99% | 0.56 | 1.72 | 4.93 | 132.5 → 124.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.98 | 12.01 | 100% | 0.57 | 2.49 | 4.88 | 110.5 → 105.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.30 | 16.64 | 99% | 0.54 | 1.82 | 4.95 | 135.8 → 128.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.10 | 16.69 | 99% | 0.53 | 1.71 | 4.25 | 135.7 → 127.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.78 | 12.08 | 100% | 0.47 | 1.43 | 3.55 | 71.8 → 68.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.285 |
| ArrangingLayoutable LineDrawing | 0.030 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable Track | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.74 | 2.94 | 4.50 | 100% | 0.45 | 2.46 | 5.38 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 189 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.15 | 18.22 | 99% | 0.56 | 1.78 | 4.91 | 131.4 → 128.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.20 | 4.70 | 70.76 | 98% | 0.70 | 4.20 | 5.04 | 343.7 → 338.4 | 6 | 1.1 / 4 |

