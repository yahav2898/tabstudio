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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 2.57 | 6.77 | 100% | 0.41 | 1.61 | 2.90 | 112.2 → 112.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.39 | 1.26 | 8.64 | 100% | 0.32 | 1.04 | 2.31 | 149.2 → 146.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 1.39 | 10.61 | 99% | 0.39 | 1.08 | 2.42 | 184.8 → 169.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.34 | 1.20 | 7.84 | 100% | 0.29 | 1.04 | 2.28 | 129.0 → 126.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.27 | 1.10 | 7.15 | 100% | 0.28 | 0.94 | 2.16 | 105.6 → 98.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.31 | 1.18 | 7.82 | 100% | 0.28 | 0.99 | 2.25 | 131.2 → 123.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.40 | 2.65 | 11.12 | 99% | 0.45 | 1.21 | 2.46 | 131.4 → 123.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.41 | 2.65 | 8.60 | 100% | 0.45 | 1.99 | 2.68 | 131.1 → 122.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.30 | 1.14 | 7.38 | 100% | 0.23 | 0.97 | 2.15 | 108.3 → 103.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.34 | 1.27 | 7.40 | 100% | 0.29 | 1.05 | 2.37 | 133.8 → 126.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.31 | 1.15 | 10.23 | 99% | 0.27 | 0.97 | 1.97 | 133.5 → 125.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 0.98 | 7.56 | 100% | 0.23 | 0.84 | 1.67 | 70.0 → 66.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.094 |
| AttachingStyle - | 0.006 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.003 |
| FindingResource - | 0.003 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.25 | 1.21 | 8.96 | 98% | 0.25 | 0.98 | 2.52 | 76.2 → 98.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 12 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.33 | 1.22 | 7.81 | 100% | 0.27 | 1.01 | 2.28 | 129.7 → 126.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.56 | 1.44 | 12.18 | 98% | 0.34 | 1.20 | 2.40 | 341.6 → 335.0 | 8 | 1.1 / 4 |

