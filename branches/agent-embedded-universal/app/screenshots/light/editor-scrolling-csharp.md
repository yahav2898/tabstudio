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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.09 | 16.74 | 99% | 0.50 | 2.62 | 4.89 | 120.2 → 121.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 3.34 | 14.76 | 99% | 0.73 | 2.78 | 5.05 | 158.4 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 4.50 | 18.68 | 99% | 1.12 | 3.02 | 5.44 | 194.4 → 178.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 3.28 | 14.14 | 99% | 0.65 | 2.75 | 4.84 | 137.8 → 135.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 3.08 | 13.71 | 99% | 0.64 | 2.61 | 4.59 | 114.2 → 107.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.82 | 13.68 | 99% | 0.54 | 2.42 | 4.44 | 139.4 → 131.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 4.47 | 17.94 | 99% | 0.59 | 2.84 | 4.59 | 139.3 → 132.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.84 | 11.82 | 99% | 0.55 | 2.45 | 4.10 | 138.9 → 130.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.60 | 8.37 | 99% | 0.42 | 2.33 | 3.97 | 117.0 → 111.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.61 | 11.83 | 99% | 0.52 | 2.20 | 4.25 | 141.7 → 134.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 3.03 | 13.82 | 99% | 0.62 | 2.58 | 3.97 | 141.8 → 134.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.49 | 14.06 | 100% | 0.46 | 2.06 | 3.57 | 79.0 → 74.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.202 |
| ArrangingLayoutable NumberDrawing | 0.046 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.78 | 3.12 | 8.99 | 98% | 0.59 | 2.51 | 5.20 | 85.1 → 106.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 236 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 3.27 | 14.63 | 99% | 0.66 | 2.73 | 4.93 | 138.8 → 135.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.57 | 5.70 | 19.87 | 98% | 0.84 | 5.03 | 5.24 | 351.1 → 344.3 | 7 | 1.1 / 4 |

