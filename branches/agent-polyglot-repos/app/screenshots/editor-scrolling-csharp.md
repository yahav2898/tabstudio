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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.72 | 9.16 | 100% | 0.36 | 1.42 | 2.93 | 118.1 → 119.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.56 | 1.86 | 10.97 | 99% | 0.46 | 1.43 | 2.93 | 156.3 → 154.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 1.89 | 12.93 | 99% | 0.57 | 1.45 | 3.16 | 191.9 → 176.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.70 | 10.04 | 99% | 0.44 | 1.39 | 3.03 | 135.9 → 133.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.53 | 8.74 | 99% | 0.39 | 1.25 | 2.74 | 112.6 → 106.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 1.71 | 12.80 | 99% | 0.47 | 1.37 | 2.95 | 138.4 → 130.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 3.18 | 14.61 | 99% | 0.56 | 2.54 | 3.39 | 138.9 → 131.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 2.02 | 11.98 | 99% | 0.50 | 1.57 | 3.32 | 137.9 → 130.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 1.96 | 9.48 | 99% | 0.47 | 1.51 | 3.00 | 115.7 → 110.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.96 | 2.32 | 11.58 | 99% | 0.57 | 1.80 | 3.58 | 142.2 → 134.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.13 | 11.02 | 99% | 0.51 | 1.68 | 3.00 | 141.2 → 133.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.58 | 6.92 | 100% | 0.43 | 1.25 | 2.26 | 77.1 → 73.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.224 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 1.80 | 2.37 | 100% | 0.38 | 1.48 | 3.45 | 83.9 → 104.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 40 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.62 | 22.10 | 99% | 0.40 | 1.35 | 2.78 | 136.5 → 133.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.78 | 2.02 | 15.08 | 98% | 0.50 | 1.62 | 2.90 | 348.9 → 342.4 | 8 | 1.1 / 4 |

