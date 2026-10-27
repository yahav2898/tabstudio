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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.23 | 11.59 | 99% | 0.40 | 1.90 | 4.28 | 119.6 → 120.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.44 | 14.06 | 99% | 0.58 | 1.97 | 4.38 | 157.1 → 154.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 2.56 | 14.97 | 99% | 0.70 | 1.92 | 4.44 | 192.3 → 176.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.54 | 12.30 | 99% | 0.57 | 2.00 | 4.57 | 137.1 → 134.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 4.01 | 12.11 | 99% | 0.66 | 2.40 | 4.51 | 114.3 → 107.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.25 | 13.03 | 99% | 0.54 | 1.83 | 4.29 | 139.3 → 131.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.21 | 13.61 | 99% | 0.53 | 1.83 | 4.31 | 139.1 → 131.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 2.24 | 12.99 | 99% | 0.54 | 1.84 | 4.16 | 138.4 → 130.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.30 | 10.95 | 99% | 0.47 | 1.85 | 4.17 | 116.8 → 112.0 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 3.28 | 84.98 | 99% | 0.66 | 2.54 | 4.63 | 142.2 → 134.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.19 | 15.38 | 99% | 0.54 | 1.80 | 3.71 | 141.8 → 133.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.81 | 10.22 | 99% | 0.44 | 1.52 | 3.10 | 77.8 → 74.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.180 |
| ArrangingLayoutable CachedLineNumberMargin | 0.048 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.57 | 2.11 | 10.34 | 98% | 0.43 | 1.71 | 4.68 | 85.0 → 106.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 30 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 3.04 | 15.98 | 99% | 0.70 | 2.45 | 4.62 | 138.0 → 135.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.09 | 2.67 | 18.79 | 98% | 0.61 | 2.15 | 4.31 | 348.5 → 342.1 | 8 | 1.1 / 4 |

