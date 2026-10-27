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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.07 | 3.15 | 12.84 | 100% | 0.57 | 2.56 | 6.12 | 124.7 → 125.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.29 | 3.23 | 19.62 | 100% | 0.78 | 2.46 | 6.11 | 162.1 → 159.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.35 | 3.22 | 21.38 | 99% | 0.87 | 2.41 | 6.08 | 196.8 → 181.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.01 | 20.30 | 99% | 0.69 | 2.39 | 5.98 | 141.9 → 139.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 1.03 | 3.21 | 17.27 | 99% | 0.74 | 2.40 | 6.19 | 118.8 → 112.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.11 | 3.02 | 19.95 | 99% | 0.72 | 2.37 | 6.06 | 144.1 → 136.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.18 | 3.11 | 20.51 | 99% | 0.72 | 2.46 | 6.26 | 143.9 → 136.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.29 | 3.48 | 22.25 | 99% | 0.83 | 2.70 | 6.45 | 143.2 → 135.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.10 | 3.04 | 14.31 | 99% | 0.64 | 2.41 | 5.97 | 121.6 → 116.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.17 | 3.09 | 21.66 | 99% | 0.71 | 2.44 | 6.22 | 146.5 → 139.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.16 | 3.03 | 21.62 | 99% | 0.72 | 2.37 | 5.51 | 146.4 → 138.6 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.82 | 2.89 | 14.81 | 100% | 0.67 | 2.18 | 4.95 | 82.1 → 78.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.277 |
| ArrangingLayoutable TextView | 0.020 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.99 | 2.82 | 3.30 | 100% | 0.54 | 2.15 | 6.43 | 88.6 → 111.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2247 ms over 187 frames at 120 Hz; the text stood still on 0 of them, 220 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.15 | 2.99 | 20.46 | 99% | 0.71 | 2.37 | 6.08 | 142.3 → 139.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.72 | 5.51 | 24.98 | 99% | 0.94 | 4.76 | 6.13 | 353.1 → 347.1 | 6 | 1.1 / 4 |

