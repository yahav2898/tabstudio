# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.43 | 1.61 | 10.25 | 100% | 0.27 | 1.36 | 3.25 | 112.1 → 113.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.78 | 10.39 | 99% | 0.44 | 1.41 | 3.29 | 150.1 → 147.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 1.83 | 12.74 | 99% | 0.52 | 1.44 | 3.41 | 185.9 → 170.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.85 | 12.31 | 99% | 0.51 | 2.39 | 3.54 | 130.3 → 127.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.83 | 7.39 | 100% | 0.39 | 1.46 | 3.36 | 107.2 → 100.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.48 | 1.59 | 9.79 | 99% | 0.40 | 1.35 | 3.38 | 132.2 → 124.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.64 | 10.35 | 99% | 0.40 | 1.38 | 3.29 | 132.0 → 124.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.48 | 1.67 | 10.41 | 99% | 0.40 | 1.36 | 3.23 | 131.3 → 123.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.57 | 9.10 | 99% | 0.34 | 1.34 | 3.15 | 109.4 → 104.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 3.09 | 12.74 | 99% | 0.53 | 2.41 | 3.59 | 135.1 → 127.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.51 | 2.42 | 8.63 | 99% | 0.41 | 2.05 | 2.95 | 135.1 → 126.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.95 | 8.42 | 100% | 0.33 | 1.76 | 2.37 | 71.3 → 67.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| ArrangingLayoutable NumberDrawing | 0.039 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.39 | 2.08 | 2.27 | 100% | 0.27 | 1.84 | 3.44 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 2.33 | 11.62 | 99% | 0.41 | 1.99 | 3.44 | 130.6 → 128.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.92 | 4.78 | 16.10 | 98% | 0.67 | 2.77 | 3.65 | 343.7 → 336.9 | 8 | 1.1 / 4 |

