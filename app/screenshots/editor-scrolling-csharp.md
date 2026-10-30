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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.17 | 35.32 | 99% | 0.47 | 2.77 | 4.60 | 86.5 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.37 | 21.28 | 99% | 0.70 | 2.80 | 4.81 | 124.2 → 122.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.58 | 17.07 | 99% | 0.91 | 2.87 | 4.81 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 3.56 | 14.36 | 99% | 0.71 | 2.90 | 5.10 | 103.9 → 101.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.34 | 5.46 | 100% | 0.72 | 2.73 | 4.86 | 81.4 → 73.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.94 | 3.67 | 18.89 | 99% | 0.79 | 2.94 | 5.27 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 3.63 | 14.60 | 100% | 0.74 | 2.99 | 5.22 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.14 | 3.79 | 16.27 | 99% | 1.00 | 3.03 | 5.31 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.98 | 3.59 | 12.44 | 99% | 0.69 | 2.92 | 4.96 | 83.9 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 3.71 | 18.66 | 99% | 0.75 | 3.05 | 5.33 | 109.2 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.04 | 3.69 | 16.78 | 99% | 0.74 | 3.01 | 4.59 | 109.2 → 100.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 3.75 | 13.43 | 100% | 1.58 | 2.71 | 3.97 | 46.5 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.309 |
| AttachingStyle - | 0.036 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.91 | 3.59 | 3.80 | 100% | 0.51 | 2.89 | 5.15 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 182 frames at 120 Hz; the text stood still on 0 of them, 217 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.10 | 3.76 | 17.26 | 99% | 0.77 | 3.07 | 5.29 | 104.9 → 101.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.67 | 6.38 | 99.54 | 98% | 2.75 | 5.47 | 5.33 | 318.5 → 310.4 | 9 | 1.1 / 4 |

