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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 1.61 | 8.87 | 99% | 0.31 | 1.35 | 3.31 | 118.1 → 119.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 3.70 | 14.52 | 99% | 0.65 | 2.47 | 3.62 | 156.3 → 154.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 1.90 | 13.21 | 99% | 0.56 | 1.45 | 3.35 | 190.9 → 175.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.68 | 9.80 | 99% | 0.40 | 1.42 | 3.22 | 135.7 → 133.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.42 | 1.67 | 8.43 | 100% | 0.40 | 1.34 | 3.33 | 113.0 → 105.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.50 | 1.76 | 12.48 | 99% | 0.42 | 1.41 | 3.43 | 138.3 → 130.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.73 | 9.77 | 99% | 0.43 | 1.41 | 3.45 | 137.9 → 130.1 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.92 | 12.07 | 99% | 0.57 | 3.49 | 3.77 | 137.6 → 130.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.10 | 8.04 | 100% | 0.48 | 1.68 | 3.54 | 116.4 → 111.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 2.27 | 10.90 | 99% | 0.58 | 1.80 | 3.87 | 141.9 → 134.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.22 | 9.76 | 99% | 0.54 | 1.72 | 3.42 | 141.4 → 133.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 1.92 | 2.53 | 100% | 0.54 | 1.51 | 3.20 | 77.6 → 74.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.315 |
| ArrangingLayoutable TextView | 0.051 |
| AttachingStyle - | 0.022 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.87 | 2.98 | 4.43 | 100% | 0.48 | 2.54 | 4.29 | 84.9 → 106.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 166 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 2.28 | 12.13 | 99% | 0.54 | 1.73 | 3.92 | 137.4 → 134.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.14 | 2.55 | 16.04 | 98% | 0.62 | 2.01 | 3.90 | 348.5 → 342.0 | 8 | 1.1 / 4 |

