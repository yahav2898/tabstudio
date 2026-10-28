# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.93 | 3.73 | 12.13 | 100% | 0.61 | 3.16 | 5.55 | 86.5 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 4.15 | 19.33 | 99% | 1.00 | 3.22 | 5.86 | 124.3 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 3.96 | 20.69 | 99% | 0.96 | 3.08 | 5.35 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 3.60 | 15.88 | 99% | 0.65 | 2.97 | 5.17 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 3.34 | 10.61 | 100% | 0.73 | 2.78 | 5.02 | 81.3 → 74.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 3.79 | 14.59 | 100% | 0.72 | 3.01 | 5.69 | 106.7 → 98.2 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.04 | 3.79 | 20.82 | 99% | 0.76 | 3.05 | 5.38 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.94 | 3.68 | 20.24 | 99% | 0.71 | 3.02 | 5.09 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 3.81 | 13.42 | 99% | 0.69 | 3.10 | 5.34 | 83.9 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 4.03 | 19.69 | 99% | 0.84 | 3.22 | 6.09 | 109.1 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 3.94 | 19.47 | 99% | 0.82 | 3.21 | 4.91 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 3.47 | 8.27 | 100% | 1.42 | 2.83 | 4.08 | 46.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.310 |
| AttachingStyle - | 0.060 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.017 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 3.65 | 4.00 | 100% | 0.58 | 3.00 | 5.34 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 171 frames at 120 Hz; the text stood still on 0 of them, 206 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 4.18 | 17.55 | 99% | 0.81 | 3.32 | 6.07 | 105.0 → 101.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.54 | 4.37 | 20.82 | 98% | 0.87 | 3.58 | 5.49 | 318.4 → 310.4 | 7 | 1.1 / 4 |

