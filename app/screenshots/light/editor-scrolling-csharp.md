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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.13 | 10.96 | 99% | 0.41 | 1.78 | 4.33 | 121.6 → 122.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.35 | 12.15 | 99% | 0.58 | 1.88 | 4.47 | 159.4 → 157.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 4.41 | 81.62 | 99% | 1.11 | 3.19 | 5.19 | 195.7 → 179.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.21 | 11.84 | 99% | 0.48 | 1.83 | 4.39 | 139.2 → 136.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.08 | 9.33 | 99% | 0.49 | 1.66 | 4.14 | 116.1 → 109.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.21 | 12.60 | 99% | 0.51 | 1.77 | 4.37 | 141.4 → 133.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.76 | 12.72 | 99% | 0.57 | 2.17 | 4.60 | 141.5 → 134.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.93 | 14.53 | 99% | 0.58 | 2.14 | 4.87 | 141.2 → 133.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.06 | 9.18 | 99% | 0.44 | 1.70 | 4.05 | 119.0 → 114.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.17 | 11.34 | 99% | 0.54 | 1.79 | 4.27 | 144.0 → 136.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.16 | 11.57 | 99% | 0.50 | 1.78 | 3.74 | 144.3 → 135.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 2.58 | 7.63 | 100% | 0.54 | 1.79 | 3.63 | 80.9 → 77.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable Track | 0.044 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 1.92 | 2.14 | 100% | 0.41 | 1.61 | 4.36 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 39 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.22 | 11.13 | 99% | 0.50 | 1.78 | 4.30 | 139.8 → 137.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.19 | 4.80 | 19.40 | 98% | 0.72 | 4.32 | 4.62 | 352.3 → 346.2 | 9 | 1.1 / 4 |

