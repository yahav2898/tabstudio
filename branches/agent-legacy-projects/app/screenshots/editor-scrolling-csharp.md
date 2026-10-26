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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 4.40 | 12.73 | 100% | 0.58 | 2.64 | 5.48 | 121.0 → 122.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 2.64 | 13.41 | 99% | 0.73 | 2.06 | 4.94 | 158.3 → 155.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 2.80 | 14.65 | 99% | 0.78 | 2.09 | 4.66 | 193.7 → 178.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.93 | 2.65 | 13.36 | 99% | 0.62 | 2.13 | 5.09 | 138.1 → 135.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 3.52 | 11.81 | 99% | 0.73 | 2.45 | 4.99 | 114.9 → 108.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.94 | 14.50 | 99% | 0.69 | 2.26 | 5.23 | 140.6 → 132.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 2.49 | 15.48 | 99% | 0.60 | 2.04 | 4.86 | 140.0 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.26 | 11.70 | 99% | 0.57 | 1.89 | 4.41 | 139.2 → 131.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 2.79 | 11.24 | 99% | 0.58 | 2.17 | 4.72 | 117.2 → 112.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 3.45 | 15.01 | 99% | 0.67 | 2.67 | 5.32 | 144.4 → 135.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 2.52 | 12.65 | 99% | 0.64 | 2.04 | 4.22 | 142.6 → 134.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.09 | 8.83 | 100% | 0.49 | 1.69 | 3.52 | 78.6 → 74.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.184 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.88 | 2.89 | 3.68 | 100% | 0.46 | 2.24 | 5.79 | 85.1 → 106.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 239 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 2.92 | 11.63 | 99% | 0.61 | 2.21 | 5.12 | 138.9 → 135.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.30 | 3.14 | 17.53 | 98% | 0.71 | 2.47 | 4.88 | 350.8 → 344.4 | 8 | 1.1 / 4 |

