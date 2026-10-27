# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.43 | 44.50 | 100% | 0.38 | 2.09 | 4.36 | 121.6 → 123.1 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.60 | 2.96 | 100% | 0.59 | 2.12 | 4.63 | 159.2 → 157.2 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.76 | 54.43 | 100% | 0.70 | 2.15 | 4.61 | 194.4 → 179.0 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.98 | 11.61 | 100% | 0.65 | 2.53 | 4.97 | 139.9 → 137.2 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.29 | 2.62 | 100% | 0.51 | 1.94 | 4.16 | 116.1 → 109.4 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.49 | 38.44 | 100% | 0.53 | 2.08 | 4.47 | 141.4 → 133.6 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.47 | 3.45 | 100% | 0.53 | 2.06 | 4.46 | 141.3 → 133.5 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.46 | 4.47 | 100% | 0.52 | 2.06 | 4.34 | 140.6 → 133.0 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 4.34 | 14.32 | 100% | 0.53 | 2.74 | 4.47 | 119.6 → 114.5 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.41 | 2.96 | 100% | 0.51 | 2.06 | 4.43 | 143.9 → 136.4 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.37 | 37.80 | 100% | 0.49 | 2.02 | 3.91 | 144.1 → 136.3 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.08 | 2.57 | 100% | 0.46 | 1.77 | 3.14 | 80.2 → 76.8 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.181 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 2.28 | 2.67 | 100% | 0.38 | 1.98 | 4.67 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 255 frames at 120 Hz; the text stood still on 0 of them, 199 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.63 | 13.81 | 100% | 0.56 | 2.18 | 4.61 | 139.8 → 136.9 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 2.86 | 54.26 | 100% | 0.59 | 2.39 | 4.57 | 350.9 → 344.5 | 2 | 1.1 / 4 |

