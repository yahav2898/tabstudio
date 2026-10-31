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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 3.36 | 15.98 | 99% | 0.49 | 2.86 | 4.76 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.53 | 19.91 | 99% | 0.76 | 2.91 | 4.73 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 4.93 | 33.03 | 99% | 2.01 | 3.05 | 5.23 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.86 | 3.53 | 18.95 | 99% | 0.72 | 2.92 | 4.83 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 3.31 | 4.57 | 100% | 0.67 | 2.74 | 4.61 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.85 | 3.43 | 18.65 | 99% | 0.71 | 2.84 | 4.92 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.85 | 3.67 | 96.76 | 99% | 0.93 | 2.92 | 4.89 | 104.2 → 96.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.40 | 17.22 | 99% | 0.67 | 2.81 | 4.64 | 103.5 → 95.4 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.86 | 3.41 | 13.56 | 100% | 0.65 | 2.82 | 4.69 | 82.2 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.98 | 3.60 | 18.15 | 99% | 0.71 | 2.95 | 5.00 | 106.8 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.92 | 3.57 | 18.01 | 99% | 0.75 | 2.90 | 4.41 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.94 | 9.07 | 100% | 1.46 | 2.47 | 3.49 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| ArrangingLayoutable CachedLineNumberMargin | 0.066 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 3.27 | 3.60 | 100% | 0.52 | 2.68 | 5.09 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 209 frames at 120 Hz; the text stood still on 0 of them, 242 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.52 | 18.65 | 99% | 0.71 | 2.88 | 4.84 | 102.7 → 99.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 3.98 | 19.92 | 98% | 0.85 | 3.24 | 4.92 | 307.6 → 299.8 | 7 | 1.1 / 4 |

