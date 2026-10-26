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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.15 | 12.68 | 99% | 0.48 | 2.77 | 4.63 | 113.4 → 114.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.09 | 4.74 | 17.75 | 99% | 0.84 | 3.24 | 5.06 | 151.7 → 150.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 3.61 | 15.73 | 99% | 0.85 | 2.94 | 4.90 | 188.0 → 171.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.27 | 11.97 | 99% | 0.59 | 2.76 | 4.67 | 131.4 → 128.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.86 | 9.29 | 99% | 0.57 | 2.48 | 4.19 | 107.9 → 101.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 3.21 | 15.74 | 99% | 0.65 | 2.67 | 4.61 | 133.5 → 125.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 3.49 | 87.70 | 99% | 0.64 | 2.85 | 4.83 | 133.9 → 125.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.02 | 3.61 | 13.04 | 99% | 0.71 | 3.07 | 5.03 | 133.9 → 125.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 3.51 | 11.77 | 99% | 0.58 | 3.03 | 4.77 | 111.2 → 106.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 3.54 | 12.13 | 99% | 0.70 | 2.93 | 4.79 | 136.4 → 128.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.61 | 78.51 | 99% | 0.68 | 2.99 | 4.39 | 136.6 → 128.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.77 | 11.56 | 99% | 0.54 | 2.47 | 3.61 | 72.7 → 69.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.204 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 2.56 | 11.32 | 98% | 0.48 | 2.16 | 4.78 | 78.2 → 100.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 3.93 | 17.86 | 99% | 0.82 | 3.14 | 5.08 | 132.8 → 130.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 3.73 | 18.89 | 98% | 0.70 | 3.17 | 4.75 | 344.3 → 338.0 | 8 | 1.1 / 4 |

