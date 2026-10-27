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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.59 | 12.83 | 99% | 0.52 | 2.06 | 4.95 | 121.8 → 123.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 2.70 | 12.67 | 99% | 0.66 | 2.13 | 4.87 | 159.3 → 157.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.16 | 2.92 | 15.14 | 99% | 0.81 | 2.21 | 5.03 | 195.4 → 179.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.12 | 4.73 | 15.96 | 99% | 0.73 | 4.07 | 5.25 | 140.4 → 137.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 2.54 | 12.16 | 99% | 0.63 | 1.92 | 4.76 | 116.6 → 109.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.99 | 2.81 | 19.75 | 99% | 0.73 | 2.18 | 5.08 | 142.1 → 134.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.89 | 2.56 | 14.94 | 99% | 0.63 | 2.00 | 4.84 | 141.5 → 133.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.08 | 3.81 | 13.70 | 99% | 0.74 | 2.75 | 5.21 | 141.7 → 134.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.87 | 2.65 | 11.08 | 99% | 0.56 | 2.06 | 4.53 | 119.2 → 114.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 2.63 | 13.05 | 99% | 0.63 | 2.12 | 4.95 | 145.0 → 136.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.11 | 2.85 | 12.21 | 99% | 0.71 | 2.19 | 4.50 | 144.2 → 136.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 2.44 | 10.05 | 100% | 0.65 | 1.89 | 3.89 | 81.3 → 77.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.399 |
| ArrangingLayoutable TextView | 0.053 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable CachedLineNumberMargin | 0.033 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 2.78 | 3.04 | 100% | 0.53 | 2.05 | 5.21 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 245 frames at 120 Hz; the text stood still on 0 of them, 266 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.10 | 3.00 | 14.43 | 99% | 0.69 | 2.36 | 5.28 | 141.0 → 138.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.42 | 3.82 | 16.77 | 98% | 0.77 | 2.95 | 4.99 | 351.9 → 345.8 | 9 | 1.1 / 4 |

