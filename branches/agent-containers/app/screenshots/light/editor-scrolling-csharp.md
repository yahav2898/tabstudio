# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 3.01 | 12.07 | 99% | 0.36 | 2.72 | 4.12 | 122.4 → 123.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 3.27 | 12.62 | 99% | 0.56 | 2.80 | 4.23 | 160.2 → 157.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 5.14 | 14.26 | 99% | 0.81 | 3.11 | 4.73 | 195.9 → 181.3 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.39 | 12.17 | 99% | 0.52 | 2.96 | 4.92 | 140.8 → 137.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.96 | 13.70 | 99% | 0.48 | 2.60 | 4.07 | 116.7 → 110.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 3.12 | 12.15 | 99% | 0.50 | 2.71 | 4.20 | 142.6 → 134.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 3.94 | 15.09 | 99% | 0.57 | 3.07 | 4.62 | 142.5 → 135.8 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.44 | 82.82 | 99% | 0.50 | 2.92 | 4.74 | 142.1 → 133.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 3.02 | 11.98 | 99% | 0.41 | 2.69 | 4.01 | 119.7 → 114.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 3.10 | 13.15 | 99% | 0.49 | 2.76 | 4.18 | 144.8 → 137.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 3.65 | 16.00 | 99% | 0.58 | 3.02 | 4.00 | 144.9 → 138.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 3.19 | 75.70 | 99% | 0.46 | 2.65 | 3.75 | 82.3 → 78.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.246 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 3.16 | 7.92 | 100% | 0.33 | 2.71 | 4.21 | 87.9 → 109.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 250 frames at 120 Hz; the text stood still on 0 of them, 260 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 3.33 | 13.35 | 99% | 0.53 | 2.89 | 4.48 | 140.8 → 138.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.23 | 5.70 | 17.70 | 98% | 0.63 | 5.40 | 5.02 | 353.9 → 346.4 | 8 | 1.1 / 4 |

