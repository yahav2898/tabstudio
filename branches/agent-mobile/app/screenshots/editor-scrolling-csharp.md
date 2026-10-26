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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.12 | 11.86 | 99% | 0.42 | 1.83 | 4.30 | 113.9 → 115.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.43 | 85.81 | 99% | 0.69 | 2.51 | 4.94 | 152.6 → 150.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 2.79 | 15.19 | 99% | 0.81 | 2.07 | 4.80 | 188.9 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.09 | 8.89 | 99% | 0.49 | 1.75 | 4.17 | 131.8 → 128.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.84 | 9.46 | 99% | 0.48 | 1.52 | 3.99 | 108.3 → 101.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 3.26 | 13.65 | 99% | 0.63 | 2.53 | 4.76 | 134.5 → 126.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 2.06 | 11.88 | 99% | 0.49 | 1.65 | 4.09 | 134.1 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.05 | 11.60 | 99% | 0.49 | 1.66 | 4.02 | 133.3 → 125.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 1.90 | 7.90 | 100% | 0.41 | 1.55 | 3.85 | 110.6 → 105.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.74 | 11.74 | 99% | 0.56 | 2.30 | 4.78 | 137.1 → 128.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 1.90 | 11.23 | 99% | 0.46 | 1.57 | 3.52 | 136.2 → 128.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.57 | 8.51 | 100% | 0.40 | 1.30 | 3.04 | 71.7 → 68.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.166 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 1.99 | 13.25 | 98% | 0.40 | 1.66 | 4.68 | 78.4 → 100.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 256 frames at 120 Hz; the text stood still on 0 of them, 206 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.09 | 10.70 | 99% | 0.51 | 1.71 | 4.17 | 132.4 → 129.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.07 | 2.51 | 16.11 | 98% | 0.60 | 1.99 | 4.16 | 345.5 → 339.1 | 8 | 1.1 / 4 |

