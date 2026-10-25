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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.28 | 1.19 | 7.21 | 100% | 0.19 | 1.03 | 2.14 | 109.1 → 110.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.36 | 1.27 | 7.57 | 100% | 0.30 | 1.08 | 2.23 | 147.1 → 144.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 1.37 | 9.12 | 99% | 0.40 | 1.10 | 2.38 | 182.7 → 167.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.32 | 1.26 | 7.48 | 100% | 0.28 | 1.07 | 2.19 | 126.8 → 124.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.32 | 2.43 | 7.52 | 100% | 0.38 | 1.12 | 2.24 | 103.8 → 97.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.38 | 2.41 | 6.99 | 100% | 0.33 | 1.40 | 2.53 | 129.8 → 121.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.32 | 1.24 | 9.68 | 100% | 0.29 | 1.05 | 2.17 | 129.0 → 121.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.32 | 1.23 | 7.70 | 100% | 0.29 | 1.04 | 2.18 | 128.4 → 120.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.30 | 1.18 | 7.12 | 100% | 0.23 | 1.01 | 2.08 | 106.2 → 101.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.32 | 1.27 | 6.66 | 100% | 0.27 | 1.06 | 2.17 | 131.6 → 124.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 2.49 | 9.84 | 100% | 0.34 | 2.16 | 1.98 | 131.6 → 124.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.51 | 4.20 | 100% | 0.31 | 1.11 | 1.82 | 68.5 → 64.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.143 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.004 |
| FindingResource - | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
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
| touch drag and flick (real touch input) | 46 | 0.27 | 1.17 | 5.80 | 100% | 0.20 | 1.00 | 2.28 | 74.2 → 95.8 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.34 | 1.64 | 8.94 | 100% | 0.28 | 1.50 | 2.17 | 127.5 → 124.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.63 | 3.77 | 13.92 | 98% | 0.41 | 3.52 | 2.45 | 340.0 → 334.0 | 9 | 1.1 / 4 |

