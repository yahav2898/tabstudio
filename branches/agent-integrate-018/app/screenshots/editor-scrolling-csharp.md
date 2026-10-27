# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.08 | 3.07 | 19.47 | 99% | 0.57 | 2.52 | 6.23 | 124.7 → 126.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 3.21 | 21.64 | 99% | 0.78 | 2.49 | 6.28 | 162.7 → 160.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.40 | 23.14 | 100% | 0.92 | 2.56 | 6.55 | 198.4 → 182.5 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.33 | 3.92 | 23.10 | 99% | 0.82 | 2.85 | 6.54 | 142.5 → 139.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.04 | 2.84 | 20.87 | 99% | 0.68 | 2.25 | 6.09 | 119.5 → 112.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.11 | 22.44 | 99% | 0.75 | 2.44 | 6.29 | 144.7 → 136.9 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.26 | 3.47 | 21.63 | 99% | 0.80 | 2.71 | 6.43 | 144.6 → 136.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.20 | 3.16 | 20.13 | 99% | 0.73 | 2.48 | 6.25 | 144.2 → 136.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.12 | 3.00 | 12.74 | 100% | 0.64 | 2.40 | 6.14 | 122.3 → 117.1 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 3.21 | 21.59 | 99% | 0.74 | 2.54 | 6.39 | 147.2 → 139.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.25 | 3.47 | 22.09 | 99% | 0.79 | 2.71 | 5.69 | 147.4 → 139.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.80 | 2.62 | 3.31 | 100% | 0.62 | 2.09 | 4.80 | 81.7 → 78.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.03 | 3.00 | 3.18 | 100% | 0.52 | 2.26 | 6.29 | 90.0 → 110.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2247 ms over 188 frames at 120 Hz; the text stood still on 0 of them, 223 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.26 | 4.80 | 82.41 | 99% | 0.79 | 2.81 | 6.27 | 143.0 → 140.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.62 | 3.54 | 25.51 | 98% | 0.83 | 2.78 | 6.25 | 354.6 → 348.2 | 6 | 1.1 / 4 |

