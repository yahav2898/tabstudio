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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.19 | 4.77 | 11.36 | 99% | 0.61 | 2.90 | 5.63 | 125.3 → 126.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.30 | 3.23 | 13.22 | 99% | 0.80 | 2.44 | 5.41 | 162.0 → 159.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.41 | 3.31 | 15.30 | 99% | 0.91 | 2.47 | 5.48 | 197.7 → 180.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.21 | 3.05 | 12.71 | 99% | 0.74 | 2.42 | 5.46 | 142.0 → 139.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.04 | 4.62 | 10.21 | 99% | 0.83 | 2.51 | 5.32 | 118.2 → 111.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.17 | 3.06 | 14.55 | 99% | 0.78 | 2.39 | 5.48 | 144.4 → 136.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.14 | 3.05 | 13.30 | 99% | 0.74 | 2.35 | 5.39 | 143.9 → 136.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.16 | 3.02 | 12.80 | 99% | 0.74 | 2.32 | 5.26 | 143.2 → 134.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.07 | 3.27 | 10.84 | 99% | 0.66 | 2.65 | 5.11 | 120.9 → 116.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.11 | 2.99 | 13.02 | 99% | 0.73 | 2.30 | 5.32 | 146.7 → 138.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 2.96 | 14.43 | 99% | 0.69 | 2.31 | 4.93 | 145.5 → 137.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.11 | 7.65 | 100% | 0.52 | 1.69 | 3.65 | 81.8 → 77.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.280 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable CachedLineNumberMargin | 0.031 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable Track | 0.019 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 2.82 | 4.47 | 100% | 0.48 | 2.31 | 5.82 | 87.7 → 109.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 245 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 2.75 | 11.11 | 99% | 0.62 | 2.13 | 5.17 | 140.9 → 138.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.41 | 3.20 | 17.96 | 98% | 0.73 | 2.54 | 5.17 | 353.0 → 346.0 | 7 | 1.1 / 4 |

