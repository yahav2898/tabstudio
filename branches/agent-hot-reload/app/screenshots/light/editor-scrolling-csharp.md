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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.43 | 11.34 | 99% | 0.45 | 2.04 | 4.60 | 112.7 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.55 | 13.84 | 99% | 0.64 | 1.97 | 4.62 | 150.5 → 148.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.76 | 16.39 | 99% | 1.14 | 2.36 | 4.85 | 185.6 → 170.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.93 | 3.12 | 11.28 | 99% | 0.61 | 2.57 | 4.96 | 131.0 → 127.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 2.37 | 10.56 | 100% | 0.57 | 1.85 | 4.49 | 107.4 → 100.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 2.67 | 15.78 | 99% | 0.69 | 2.08 | 4.91 | 132.7 → 124.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 2.38 | 14.96 | 99% | 0.57 | 1.91 | 4.48 | 132.7 → 124.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 3.31 | 12.78 | 99% | 0.65 | 2.41 | 4.78 | 132.5 → 124.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.32 | 10.33 | 99% | 0.49 | 1.90 | 4.35 | 110.1 → 105.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.42 | 11.59 | 99% | 0.58 | 1.98 | 4.56 | 135.1 → 127.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.27 | 12.14 | 99% | 0.54 | 1.87 | 3.91 | 135.1 → 127.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.40 | 8.37 | 100% | 0.56 | 1.87 | 3.69 | 71.4 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.363 |
| AttachingStyle - | 0.054 |
| RaisingRoutedEvent ScrollBar | 0.042 |
| ArrangingLayoutable TextView | 0.029 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable Grid | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 2.24 | 11.51 | 98% | 0.45 | 1.94 | 4.59 | 78.1 → 99.8 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 82 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 2.66 | 13.92 | 99% | 0.68 | 2.07 | 4.79 | 131.1 → 128.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 3.09 | 18.84 | 98% | 0.68 | 2.42 | 4.67 | 341.9 → 335.7 | 9 | 1.1 / 4 |

