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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.81 | 2.57 | 9.20 | 99% | 0.51 | 2.06 | 5.00 | 113.9 → 114.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.98 | 2.63 | 11.27 | 99% | 0.68 | 2.00 | 4.64 | 151.5 → 149.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 2.78 | 14.51 | 99% | 0.81 | 2.08 | 4.72 | 187.3 → 171.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 3.80 | 10.25 | 99% | 0.79 | 2.64 | 5.24 | 131.9 → 129.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.40 | 10.29 | 99% | 0.59 | 1.84 | 4.45 | 107.8 → 100.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.50 | 14.03 | 99% | 0.62 | 1.94 | 4.53 | 133.4 → 125.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.48 | 11.38 | 99% | 0.62 | 1.96 | 4.64 | 133.3 → 125.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.91 | 2.94 | 12.24 | 99% | 0.65 | 2.48 | 4.90 | 133.3 → 124.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.33 | 7.72 | 100% | 0.52 | 1.84 | 4.28 | 110.5 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.33 | 10.56 | 99% | 0.62 | 1.89 | 4.49 | 136.0 → 128.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 3.02 | 10.68 | 99% | 0.62 | 2.44 | 4.20 | 135.9 → 128.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.16 | 7.48 | 100% | 0.54 | 1.67 | 3.80 | 71.8 → 68.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 2.32 | 2.38 | 100% | 0.43 | 1.80 | 4.71 | 78.2 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 68 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.41 | 17.24 | 99% | 0.86 | 2.29 | 4.93 | 131.8 → 129.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.31 | 2.98 | 16.64 | 98% | 0.72 | 2.29 | 4.60 | 344.1 → 337.6 | 8 | 1.1 / 4 |

