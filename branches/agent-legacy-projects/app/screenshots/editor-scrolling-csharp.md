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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.84 | 10.18 | 99% | 0.40 | 2.54 | 4.52 | 120.0 → 121.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.87 | 13.68 | 99% | 0.56 | 2.45 | 4.31 | 157.9 → 155.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 3.04 | 14.10 | 99% | 0.70 | 2.52 | 4.42 | 193.7 → 177.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 3.10 | 13.51 | 99% | 0.58 | 2.53 | 4.50 | 137.8 → 135.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.79 | 74.46 | 99% | 0.59 | 2.42 | 4.40 | 115.1 → 107.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.52 | 14.09 | 99% | 0.53 | 2.25 | 4.24 | 139.9 → 132.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.81 | 11.07 | 99% | 0.50 | 2.42 | 4.25 | 140.2 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.97 | 12.04 | 99% | 0.61 | 2.49 | 4.44 | 139.3 → 131.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.84 | 12.60 | 99% | 0.46 | 2.48 | 4.19 | 117.7 → 112.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.60 | 10.61 | 99% | 0.52 | 2.30 | 4.27 | 142.6 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.66 | 11.35 | 99% | 0.49 | 2.38 | 3.69 | 142.6 → 134.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.28 | 9.44 | 100% | 0.42 | 2.04 | 3.12 | 78.5 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.254 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.67 | 3.95 | 4.94 | 100% | 0.38 | 3.75 | 4.56 | 85.2 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 79 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.85 | 11.52 | 99% | 0.52 | 2.42 | 4.31 | 138.2 → 135.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.13 | 3.26 | 18.32 | 98% | 0.64 | 2.85 | 4.43 | 350.5 → 344.4 | 9 | 1.1 / 4 |

