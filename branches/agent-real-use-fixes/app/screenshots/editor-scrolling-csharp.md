# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 4.74 | 114.72 | 99% | 0.56 | 3.36 | 5.23 | 123.9 → 124.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.06 | 2.93 | 15.80 | 99% | 0.72 | 2.25 | 4.94 | 160.9 → 158.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.14 | 3.00 | 17.80 | 99% | 0.82 | 2.30 | 4.94 | 196.8 → 181.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 2.83 | 14.75 | 99% | 0.66 | 2.26 | 4.94 | 140.7 → 137.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.68 | 14.45 | 99% | 0.67 | 2.10 | 4.70 | 117.0 → 110.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.07 | 3.72 | 18.89 | 99% | 2.48 | 2.65 | 5.17 | 143.5 → 135.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.92 | 2.64 | 22.56 | 99% | 0.65 | 2.21 | 4.82 | 142.6 → 134.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 2.73 | 14.51 | 99% | 0.68 | 2.16 | 4.66 | 141.6 → 133.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 2.69 | 14.52 | 99% | 0.59 | 2.19 | 4.68 | 119.6 → 114.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 2.84 | 15.23 | 100% | 0.65 | 2.24 | 4.98 | 145.2 → 137.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.11 | 4.82 | 18.94 | 99% | 2.49 | 4.15 | 4.59 | 145.5 → 138.3 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.19 | 10.30 | 99% | 0.56 | 1.80 | 3.52 | 81.4 → 77.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| FindingResource - | 0.064 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 2.12 | 2.27 | 100% | 0.42 | 1.78 | 4.49 | 87.3 → 109.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 127 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.48 | 13.85 | 99% | 0.58 | 1.97 | 4.58 | 140.6 → 137.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.37 | 4.83 | 22.95 | 98% | 0.93 | 2.92 | 4.86 | 353.5 → 348.3 | 9 | 1.1 / 4 |

