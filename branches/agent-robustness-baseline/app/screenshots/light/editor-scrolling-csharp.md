# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.06 | 13.31 | 99% | 0.44 | 2.70 | 4.50 | 86.7 → 87.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 4.62 | 19.19 | 99% | 1.21 | 2.93 | 4.74 | 124.6 → 122.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.71 | 19.71 | 99% | 0.89 | 2.91 | 4.84 | 160.2 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.35 | 16.35 | 99% | 0.60 | 2.83 | 4.73 | 104.2 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.07 | 14.77 | 99% | 0.59 | 2.61 | 4.46 | 81.1 → 73.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.03 | 3.79 | 94.15 | 99% | 0.86 | 3.09 | 5.24 | 106.8 → 98.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.71 | 16.01 | 99% | 0.75 | 3.02 | 4.94 | 106.7 → 98.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.59 | 15.64 | 99% | 0.69 | 2.99 | 4.71 | 106.0 → 97.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 3.61 | 12.81 | 100% | 0.68 | 2.97 | 4.81 | 83.9 → 78.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.01 | 3.63 | 12.85 | 99% | 0.75 | 3.00 | 5.03 | 109.4 → 101.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 3.69 | 18.45 | 99% | 0.74 | 2.99 | 4.68 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.63 | 3.18 | 11.95 | 100% | 1.20 | 2.63 | 3.64 | 46.1 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.380 |
| AttachingStyle - | 0.024 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.88 | 3.67 | 5.15 | 100% | 0.56 | 3.21 | 5.19 | 50.3 → 72.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 179 frames at 120 Hz; the text stood still on 0 of them, 214 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.95 | 3.60 | 16.04 | 99% | 0.70 | 2.94 | 4.97 | 105.2 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 4.00 | 18.75 | 98% | 0.80 | 3.19 | 4.88 | 318.8 → 310.7 | 7 | 1.1 / 4 |

