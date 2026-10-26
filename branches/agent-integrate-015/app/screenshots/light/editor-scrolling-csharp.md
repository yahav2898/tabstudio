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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 2.45 | 12.25 | 99% | 0.51 | 2.06 | 4.80 | 117.4 → 118.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 2.85 | 13.28 | 99% | 0.72 | 2.22 | 4.96 | 155.2 → 153.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 2.88 | 15.89 | 99% | 0.83 | 2.19 | 4.90 | 190.8 → 174.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 4.57 | 13.40 | 99% | 0.81 | 2.90 | 5.18 | 135.5 → 132.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.56 | 12.69 | 99% | 0.66 | 1.96 | 4.71 | 111.6 → 105.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 2.88 | 16.12 | 99% | 0.71 | 2.21 | 5.01 | 137.5 → 130.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 2.86 | 14.49 | 99% | 0.72 | 2.24 | 5.08 | 137.2 → 130.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 4.45 | 87.48 | 99% | 0.80 | 2.70 | 5.15 | 137.2 → 129.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 2.85 | 10.70 | 99% | 0.63 | 2.24 | 4.80 | 115.0 → 110.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.11 | 2.97 | 12.72 | 99% | 0.73 | 2.27 | 5.05 | 140.3 → 133.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 2.87 | 12.52 | 99% | 0.69 | 2.26 | 4.43 | 139.7 → 131.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.79 | 2.93 | 11.48 | 100% | 0.69 | 2.27 | 3.99 | 76.9 → 73.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 2.84 | 3.08 | 100% | 0.53 | 2.17 | 5.20 | 82.7 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 234 frames at 120 Hz; the text stood still on 0 of them, 267 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.12 | 3.00 | 12.20 | 99% | 0.74 | 2.33 | 5.09 | 136.4 → 133.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.86 | 5.34 | 21.08 | 98% | 1.01 | 4.66 | 5.19 | 348.5 → 342.0 | 9 | 1.1 / 4 |

