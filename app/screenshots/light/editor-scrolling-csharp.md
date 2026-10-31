# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 3.41 | 14.53 | 100% | 0.53 | 2.87 | 4.75 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.66 | 18.23 | 99% | 0.75 | 3.01 | 4.84 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 3.87 | 18.08 | 99% | 1.12 | 2.98 | 5.00 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.81 | 28.21 | 99% | 0.79 | 3.05 | 5.11 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 3.43 | 37.33 | 99% | 0.68 | 2.84 | 4.86 | 79.8 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 3.27 | 14.61 | 99% | 0.65 | 2.70 | 4.62 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.88 | 3.48 | 22.82 | 99% | 0.72 | 2.87 | 4.95 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.78 | 3.37 | 17.21 | 99% | 0.64 | 2.78 | 4.64 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.87 | 3.52 | 16.40 | 100% | 0.70 | 2.89 | 4.91 | 82.3 → 77.0 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.97 | 3.91 | 11.92 | 100% | 0.74 | 3.03 | 5.10 | 106.8 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.91 | 3.62 | 17.24 | 99% | 0.72 | 2.96 | 4.26 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.63 | 3.23 | 11.85 | 100% | 1.27 | 2.62 | 3.69 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| AttachingStyle - | 0.067 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.98 | 3.43 | 3.68 | 100% | 0.50 | 2.81 | 5.33 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 176 frames at 120 Hz; the text stood still on 0 of them, 210 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.57 | 14.27 | 99% | 0.65 | 2.95 | 4.93 | 102.7 → 99.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 4.10 | 21.88 | 98% | 0.83 | 3.39 | 5.18 | 307.7 → 299.8 | 7 | 1.1 / 4 |

