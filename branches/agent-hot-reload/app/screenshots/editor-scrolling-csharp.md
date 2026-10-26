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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 3.01 | 10.30 | 99% | 0.59 | 2.45 | 5.21 | 114.4 → 116.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.25 | 3.24 | 14.10 | 99% | 0.80 | 2.39 | 5.22 | 153.4 → 151.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.32 | 3.15 | 15.50 | 99% | 0.92 | 2.29 | 5.12 | 189.6 → 173.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 2.89 | 15.18 | 99% | 0.73 | 2.23 | 5.09 | 132.5 → 129.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.99 | 4.32 | 14.30 | 99% | 0.77 | 2.56 | 5.03 | 109.3 → 102.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.05 | 2.87 | 12.61 | 99% | 0.72 | 2.26 | 5.11 | 134.9 → 126.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.10 | 2.94 | 16.69 | 99% | 0.74 | 2.20 | 5.06 | 134.6 → 126.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.14 | 3.23 | 13.79 | 99% | 0.77 | 2.38 | 5.15 | 134.0 → 126.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.05 | 2.97 | 86.00 | 99% | 0.66 | 2.27 | 4.85 | 111.6 → 106.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.11 | 2.89 | 13.43 | 99% | 0.73 | 2.23 | 5.17 | 137.6 → 129.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.10 | 2.84 | 14.17 | 99% | 0.72 | 2.22 | 4.60 | 136.9 → 129.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.82 | 2.83 | 11.85 | 99% | 0.67 | 2.27 | 4.03 | 72.8 → 69.6 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.320 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.96 | 2.82 | 12.29 | 98% | 0.66 | 2.18 | 5.22 | 78.6 → 100.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 234 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 3.03 | 13.37 | 99% | 0.73 | 2.27 | 5.23 | 133.4 → 130.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.81 | 5.58 | 84.89 | 98% | 1.04 | 4.74 | 5.26 | 347.5 → 341.0 | 9 | 1.1 / 4 |

