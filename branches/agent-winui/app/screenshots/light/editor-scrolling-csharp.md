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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.29 | 1.64 | 8.22 | 100% | 0.19 | 1.50 | 2.20 | 108.8 → 110.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.38 | 1.69 | 8.57 | 100% | 0.31 | 1.52 | 2.26 | 146.3 → 144.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 1.77 | 8.96 | 99% | 0.37 | 1.54 | 2.34 | 181.6 → 165.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.33 | 1.66 | 8.36 | 100% | 0.27 | 1.52 | 2.22 | 126.2 → 123.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.35 | 2.41 | 7.18 | 100% | 0.39 | 1.59 | 2.30 | 103.3 → 96.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 2.10 | 9.78 | 100% | 0.35 | 1.65 | 2.67 | 129.1 → 120.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.32 | 1.68 | 10.21 | 100% | 0.28 | 1.53 | 2.24 | 128.4 → 120.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.32 | 1.53 | 7.68 | 100% | 0.30 | 1.41 | 2.20 | 127.7 → 119.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.31 | 1.66 | 6.91 | 100% | 0.23 | 1.51 | 2.13 | 106.0 → 101.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.36 | 1.75 | 7.35 | 100% | 0.31 | 1.53 | 2.35 | 131.2 → 123.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.43 | 2.80 | 55.18 | 99% | 0.46 | 2.36 | 2.41 | 131.5 → 123.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.46 | 4.99 | 100% | 0.23 | 1.38 | 1.59 | 67.9 → 64.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.142 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| FindingResource - | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable Grid | 0.000 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.26 | 1.32 | 1.46 | 100% | 0.20 | 1.10 | 2.33 | 74.0 → 94.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 0 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.32 | 1.23 | 7.98 | 100% | 0.28 | 1.04 | 2.23 | 126.9 → 123.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.69 | 3.77 | 17.58 | 99% | 2.25 | 3.43 | 2.75 | 338.6 → 331.9 | 8 | 1.1 / 4 |

