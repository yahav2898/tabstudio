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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 1.91 | 9.49 | 99% | 0.33 | 1.66 | 4.03 | 121.8 → 122.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.18 | 11.86 | 99% | 0.53 | 1.70 | 4.20 | 159.3 → 157.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.27 | 14.29 | 99% | 0.65 | 1.75 | 4.23 | 194.4 → 178.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.74 | 79.28 | 99% | 0.63 | 2.51 | 4.78 | 139.4 → 137.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.88 | 9.75 | 99% | 0.46 | 1.54 | 3.88 | 115.9 → 109.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.61 | 2.01 | 13.50 | 99% | 0.50 | 1.67 | 4.11 | 141.2 → 133.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.01 | 12.33 | 99% | 0.49 | 1.67 | 4.12 | 141.3 → 133.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.06 | 12.06 | 99% | 0.50 | 1.69 | 3.96 | 140.6 → 132.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.91 | 81.94 | 99% | 0.50 | 2.58 | 4.07 | 119.0 → 114.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.05 | 12.60 | 99% | 0.49 | 1.69 | 4.10 | 143.9 → 136.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.02 | 11.17 | 99% | 0.47 | 1.66 | 3.64 | 143.6 → 135.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.84 | 8.37 | 99% | 0.44 | 1.52 | 3.14 | 79.9 → 76.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 2.12 | 9.39 | 98% | 0.41 | 1.73 | 4.54 | 87.2 → 107.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 121 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.29 | 11.65 | 99% | 0.54 | 1.81 | 4.24 | 139.8 → 137.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 2.49 | 15.72 | 98% | 0.59 | 2.02 | 4.20 | 350.7 → 344.2 | 8 | 1.1 / 4 |

