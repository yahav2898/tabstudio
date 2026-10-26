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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.32 | 1.24 | 8.46 | 100% | 0.22 | 1.06 | 2.35 | 111.9 → 113.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 1.33 | 7.68 | 100% | 0.34 | 1.08 | 2.42 | 150.5 → 148.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.47 | 1.41 | 11.17 | 99% | 0.42 | 1.12 | 2.50 | 186.6 → 170.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.37 | 1.25 | 7.76 | 100% | 0.31 | 1.09 | 2.39 | 130.0 → 127.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.34 | 1.65 | 10.08 | 100% | 0.41 | 1.14 | 2.73 | 106.5 → 100.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.50 | 3.28 | 11.51 | 99% | 0.61 | 2.43 | 3.20 | 132.9 → 125.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 1.45 | 11.67 | 99% | 0.34 | 1.17 | 2.68 | 132.4 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.29 | 8.75 | 99% | 0.32 | 1.08 | 2.36 | 131.3 → 123.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.22 | 7.38 | 100% | 0.25 | 1.05 | 2.30 | 109.1 → 104.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.37 | 1.37 | 9.08 | 99% | 0.33 | 1.13 | 2.50 | 134.8 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 1.34 | 7.89 | 100% | 0.37 | 1.12 | 2.36 | 134.9 → 126.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.11 | 8.34 | 100% | 0.26 | 0.94 | 1.82 | 70.5 → 66.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.213 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.78 | 2.96 | 3.02 | 100% | 0.53 | 2.40 | 3.44 | 77.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 59 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 1.93 | 13.64 | 99% | 0.51 | 1.50 | 3.27 | 131.4 → 128.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.95 | 2.28 | 15.91 | 98% | 0.57 | 1.71 | 3.23 | 345.0 → 338.1 | 8 | 1.1 / 4 |

