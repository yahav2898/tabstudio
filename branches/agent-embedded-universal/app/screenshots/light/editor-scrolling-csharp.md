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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 1.98 | 11.16 | 99% | 0.39 | 1.64 | 3.68 | 119.2 → 120.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 2.18 | 12.05 | 99% | 0.55 | 1.70 | 3.70 | 156.7 → 154.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.44 | 14.13 | 99% | 0.66 | 1.78 | 3.81 | 192.2 → 176.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.28 | 86.04 | 99% | 0.58 | 2.67 | 3.89 | 137.1 → 134.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.91 | 9.93 | 99% | 0.48 | 1.53 | 3.53 | 113.1 → 106.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.01 | 14.26 | 99% | 0.49 | 1.55 | 3.56 | 138.6 → 130.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.03 | 12.38 | 99% | 0.49 | 1.63 | 3.66 | 138.5 → 130.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.04 | 11.58 | 99% | 0.51 | 1.63 | 3.59 | 137.5 → 130.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 2.98 | 11.51 | 99% | 0.51 | 1.89 | 3.64 | 115.9 → 111.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.73 | 12.31 | 99% | 0.54 | 2.09 | 4.04 | 141.9 → 133.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 1.90 | 12.51 | 99% | 0.47 | 1.56 | 3.18 | 140.5 → 133.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.68 | 8.38 | 100% | 0.42 | 1.36 | 2.65 | 77.6 → 73.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.178 |
| ArrangingLayoutable TextView | 0.049 |
| FindingResource - | 0.010 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 1.92 | 7.85 | 100% | 0.39 | 1.55 | 3.68 | 83.7 → 105.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.09 | 11.78 | 99% | 0.61 | 2.03 | 3.83 | 137.0 → 134.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.04 | 2.97 | 15.88 | 98% | 0.58 | 2.14 | 3.91 | 348.5 → 341.9 | 7 | 1.1 / 4 |

