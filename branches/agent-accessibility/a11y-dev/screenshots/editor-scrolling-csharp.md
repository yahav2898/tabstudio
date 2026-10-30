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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 3.13 | 13.79 | 100% | 0.42 | 2.72 | 4.73 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.09 | 3.99 | 18.35 | 99% | 0.84 | 3.07 | 5.37 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 3.73 | 19.17 | 99% | 0.96 | 2.95 | 5.06 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.37 | 15.44 | 99% | 0.62 | 2.83 | 4.77 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.12 | 13.61 | 100% | 0.63 | 2.63 | 4.59 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 3.57 | 15.73 | 99% | 0.79 | 2.91 | 4.87 | 104.4 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.81 | 3.39 | 17.50 | 99% | 0.65 | 2.84 | 4.86 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.60 | 13.99 | 99% | 0.67 | 2.94 | 4.86 | 103.5 → 95.4 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.85 | 3.64 | 11.93 | 99% | 0.64 | 2.96 | 4.83 | 82.0 → 76.7 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.88 | 3.54 | 18.62 | 99% | 0.74 | 3.01 | 5.31 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 3.24 | 22.31 | 99% | 0.60 | 2.80 | 4.22 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 3.01 | 12.32 | 100% | 1.21 | 2.57 | 3.58 | 45.6 → 42.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 61.3

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.255 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.89 | 3.35 | 3.68 | 100% | 0.54 | 2.85 | 6.08 | 49.2 → 73.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 214 frames at 120 Hz; the text stood still on 0 of them, 249 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.37 | 21.16 | 99% | 0.68 | 2.86 | 4.95 | 102.6 → 99.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.28 | 3.91 | 19.98 | 99% | 0.73 | 3.24 | 5.11 | 306.9 → 299.1 | 7 | 1.1 / 4 |

