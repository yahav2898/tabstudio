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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.50 | 13.49 | 100% | 0.43 | 2.11 | 3.14 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 3.38 | 13.77 | 100% | 1.44 | 2.31 | 3.23 | 121.3 → 121.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 4.25 | 104.21 | 99% | 2.48 | 2.50 | 3.39 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.49 | 2.29 | 15.45 | 99% | 0.44 | 1.95 | 2.89 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.35 | 2.11 | 11.08 | 100% | 0.43 | 1.78 | 2.50 | 79.7 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 2.45 | 15.66 | 99% | 0.55 | 2.03 | 3.11 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.52 | 2.30 | 17.51 | 100% | 0.46 | 1.99 | 2.94 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.52 | 2.38 | 14.91 | 100% | 0.50 | 2.00 | 2.94 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.53 | 2.43 | 13.86 | 99% | 0.42 | 2.06 | 2.86 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.62 | 2.80 | 16.70 | 99% | 1.30 | 2.26 | 3.11 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.76 | 86.63 | 99% | 0.51 | 2.23 | 2.90 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 1.87 | 17.56 | 100% | 0.58 | 1.66 | 1.82 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.147 |
| AttachingStyle - | 0.062 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.62 | 2.41 | 2.50 | 100% | 0.42 | 1.95 | 3.10 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 26 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.50 | 2.44 | 22.77 | 99% | 0.48 | 1.99 | 3.01 | 102.7 → 99.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.81 | 4.52 | 26.46 | 98% | 1.33 | 4.11 | 3.22 | 307.6 → 299.7 | 8 | 1.1 / 4 |

