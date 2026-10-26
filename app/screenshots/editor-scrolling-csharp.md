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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.51 | 12.36 | 99% | 0.51 | 2.03 | 4.27 | 119.8 → 121.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.28 | 12.96 | 99% | 0.59 | 1.85 | 3.96 | 157.5 → 155.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.49 | 11.59 | 99% | 0.71 | 1.89 | 4.10 | 193.4 → 177.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.57 | 11.82 | 99% | 0.58 | 1.99 | 4.02 | 137.3 → 134.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.85 | 10.93 | 100% | 0.60 | 2.33 | 3.89 | 114.6 → 107.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.16 | 9.14 | 99% | 0.54 | 1.74 | 3.80 | 139.6 → 131.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.88 | 2.97 | 18.71 | 99% | 0.62 | 2.59 | 4.25 | 139.7 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.97 | 11.84 | 99% | 0.56 | 2.50 | 3.95 | 139.3 → 131.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.73 | 2.78 | 12.98 | 99% | 0.49 | 2.38 | 3.74 | 117.5 → 112.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 3.40 | 15.47 | 99% | 0.66 | 2.75 | 4.22 | 142.9 → 135.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 3.11 | 13.61 | 99% | 0.57 | 2.57 | 3.62 | 142.6 → 134.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.27 | 7.58 | 100% | 0.44 | 2.05 | 2.81 | 78.2 → 74.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| MeasuringLayoutable LineDrawing | 0.041 |
| ArrangingLayoutable Border | 0.039 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 3.08 | 3.19 | 100% | 0.44 | 2.54 | 4.45 | 85.1 → 105.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 144 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 4.41 | 13.07 | 99% | 0.71 | 2.91 | 4.52 | 138.7 → 136.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 3.80 | 19.06 | 98% | 0.68 | 3.13 | 4.34 | 352.0 → 344.4 | 8 | 1.1 / 4 |

