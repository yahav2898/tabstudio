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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.18 | 13.18 | 99% | 0.49 | 2.76 | 4.43 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.33 | 19.70 | 99% | 0.65 | 2.81 | 4.52 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 5.96 | 36.73 | 99% | 2.73 | 3.22 | 5.70 | 156.8 → 141.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.33 | 15.44 | 99% | 0.64 | 2.78 | 4.57 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 3.29 | 17.12 | 99% | 0.67 | 2.67 | 4.62 | 79.8 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.77 | 3.38 | 4.71 | 100% | 0.71 | 2.82 | 4.84 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.81 | 3.35 | 16.15 | 99% | 0.66 | 2.81 | 4.74 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.92 | 3.76 | 16.88 | 99% | 0.89 | 2.95 | 4.94 | 103.6 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 3.19 | 13.09 | 100% | 0.53 | 2.72 | 4.40 | 82.3 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.70 | 3.22 | 16.52 | 99% | 0.61 | 2.73 | 4.51 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.81 | 3.41 | 14.26 | 99% | 0.66 | 2.82 | 4.10 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.11 | 12.91 | 100% | 1.60 | 2.60 | 3.69 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.411 |
| AttachingStyle - | 0.029 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| ArrangingLayoutable CachedLineNumberMargin | 0.022 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 3.39 | 3.48 | 100% | 0.49 | 2.79 | 4.85 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 212 frames at 120 Hz; the text stood still on 0 of them, 245 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 3.58 | 22.35 | 99% | 0.74 | 2.92 | 5.06 | 102.6 → 99.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.84 | 6.07 | 106.08 | 98% | 2.63 | 5.41 | 5.32 | 307.7 → 299.9 | 8 | 1.1 / 4 |

