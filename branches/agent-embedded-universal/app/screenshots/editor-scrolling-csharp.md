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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.13 | 8.84 | 100% | 0.40 | 1.83 | 4.36 | 119.8 → 121.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 4.48 | 91.83 | 99% | 0.73 | 2.74 | 4.89 | 158.0 → 157.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 2.59 | 15.48 | 99% | 0.71 | 2.00 | 4.51 | 193.7 → 177.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.31 | 11.37 | 99% | 0.55 | 1.89 | 4.43 | 137.6 → 134.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.17 | 10.64 | 99% | 0.51 | 1.76 | 4.20 | 114.6 → 107.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.65 | 15.34 | 99% | 0.60 | 2.02 | 4.61 | 139.9 → 132.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.86 | 10.65 | 99% | 0.60 | 2.40 | 4.91 | 140.6 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.22 | 12.02 | 99% | 0.56 | 1.82 | 4.33 | 139.1 → 131.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 2.18 | 10.31 | 99% | 0.46 | 1.81 | 4.13 | 117.2 → 112.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 2.26 | 11.41 | 99% | 0.54 | 1.85 | 4.36 | 142.4 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 4.10 | 15.19 | 99% | 2.05 | 2.62 | 4.37 | 142.9 → 135.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.90 | 6.84 | 100% | 0.48 | 1.60 | 3.33 | 78.2 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| AttachingStyle - | 0.053 |
| ArrangingLayoutable LineDrawing | 0.044 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.63 | 2.07 | 2.20 | 100% | 0.41 | 1.70 | 4.39 | 85.1 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 80 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 2.65 | 11.69 | 99% | 0.59 | 2.00 | 4.77 | 138.4 → 135.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.26 | 3.56 | 17.08 | 98% | 0.69 | 2.95 | 4.99 | 351.5 → 343.8 | 8 | 1.1 / 4 |

