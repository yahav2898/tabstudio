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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.97 | 14.14 | 99% | 0.48 | 2.39 | 3.93 | 119.6 → 120.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.95 | 87.56 | 99% | 0.63 | 1.97 | 3.98 | 157.0 → 154.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 2.33 | 15.29 | 99% | 0.68 | 1.73 | 3.92 | 191.6 → 176.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.13 | 10.13 | 99% | 0.51 | 1.68 | 3.85 | 136.2 → 133.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.03 | 9.81 | 99% | 0.52 | 1.59 | 3.79 | 112.7 → 106.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.11 | 13.89 | 99% | 0.53 | 1.69 | 3.83 | 139.0 → 130.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 4.14 | 14.71 | 99% | 0.86 | 2.72 | 4.23 | 139.6 → 131.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.33 | 11.98 | 99% | 0.58 | 1.83 | 4.16 | 138.1 → 130.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.07 | 11.37 | 99% | 0.50 | 1.66 | 3.66 | 115.9 → 111.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.10 | 11.00 | 99% | 0.52 | 1.67 | 3.83 | 141.2 → 133.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 1.91 | 10.57 | 99% | 0.47 | 1.57 | 3.17 | 140.6 → 132.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.61 | 7.82 | 100% | 0.40 | 1.32 | 2.61 | 77.1 → 73.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.284 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable TextView | 0.028 |
| ArrangingLayoutable ScrollingTextLayer | 0.027 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.76 | 2.65 | 11.00 | 98% | 0.44 | 2.29 | 3.96 | 84.9 → 106.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 116 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.05 | 10.28 | 99% | 0.47 | 1.63 | 3.76 | 137.3 → 133.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.92 | 2.36 | 14.61 | 98% | 0.57 | 1.86 | 3.83 | 348.3 → 341.8 | 7 | 1.1 / 4 |

