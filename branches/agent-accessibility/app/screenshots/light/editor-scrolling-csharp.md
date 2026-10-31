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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.43 | 2.22 | 11.41 | 100% | 0.31 | 1.98 | 2.68 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 4.40 | 15.57 | 100% | 1.73 | 2.57 | 3.58 | 121.2 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.19 | 16.97 | 99% | 1.60 | 2.42 | 3.75 | 156.7 → 141.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 2.83 | 16.73 | 99% | 0.63 | 2.26 | 3.40 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.36 | 2.06 | 9.29 | 100% | 0.36 | 1.81 | 2.45 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.40 | 2.20 | 14.62 | 99% | 0.41 | 1.92 | 2.83 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.42 | 2.25 | 13.51 | 99% | 0.42 | 1.94 | 2.71 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.40 | 2.21 | 14.28 | 99% | 0.39 | 1.89 | 2.54 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.52 | 2.59 | 14.94 | 99% | 0.47 | 2.07 | 2.75 | 82.3 → 76.9 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.53 | 2.68 | 13.31 | 99% | 0.49 | 2.15 | 3.19 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.39 | 2.20 | 11.68 | 99% | 0.37 | 1.89 | 2.29 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 2.02 | 9.56 | 100% | 1.00 | 1.75 | 1.84 | 46.0 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.218 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.43 | 2.21 | 2.30 | 100% | 0.34 | 1.89 | 2.70 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 18 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.78 | 20.94 | 99% | 0.58 | 2.20 | 3.13 | 102.6 → 99.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.87 | 5.12 | 90.99 | 98% | 1.30 | 4.47 | 3.39 | 307.6 → 299.8 | 7 | 1.1 / 4 |

