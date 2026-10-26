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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.48 | 10.66 | 99% | 0.50 | 2.00 | 4.67 | 113.9 → 115.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.06 | 2.72 | 12.69 | 99% | 0.71 | 2.09 | 4.81 | 152.4 → 150.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 4.59 | 14.02 | 99% | 1.35 | 2.74 | 5.24 | 190.1 → 172.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 2.55 | 10.91 | 99% | 0.61 | 2.01 | 4.57 | 131.8 → 129.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 2.29 | 9.55 | 99% | 0.62 | 1.80 | 4.42 | 108.4 → 101.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.95 | 2.71 | 13.52 | 99% | 0.67 | 2.02 | 4.83 | 134.3 → 126.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 4.35 | 16.15 | 99% | 0.81 | 2.26 | 5.28 | 135.0 → 126.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 2.57 | 13.92 | 99% | 0.67 | 2.03 | 4.63 | 133.4 → 125.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 2.44 | 9.39 | 100% | 0.56 | 1.95 | 4.49 | 110.9 → 106.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 3.15 | 15.08 | 99% | 0.70 | 2.36 | 5.11 | 136.8 → 130.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 3.01 | 9.97 | 99% | 0.66 | 2.27 | 4.94 | 137.3 → 128.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 2.06 | 6.02 | 100% | 0.53 | 1.62 | 3.52 | 72.0 → 68.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.293 |
| AttachingStyle - | 0.046 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.83 | 2.27 | 2.44 | 100% | 0.51 | 1.75 | 4.90 | 78.4 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 171 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 3.28 | 13.29 | 99% | 0.73 | 2.44 | 5.42 | 133.3 → 129.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.35 | 3.05 | 15.81 | 98% | 0.74 | 2.33 | 4.68 | 346.1 → 339.5 | 7 | 1.1 / 4 |

