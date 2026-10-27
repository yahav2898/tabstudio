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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 3.09 | 8.21 | 100% | 0.48 | 2.49 | 4.80 | 122.5 → 123.7 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.42 | 16.18 | 99% | 0.59 | 1.93 | 4.53 | 159.5 → 157.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 2.72 | 15.67 | 99% | 0.76 | 2.02 | 4.73 | 194.6 → 179.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.32 | 12.25 | 99% | 0.55 | 1.87 | 4.47 | 139.4 → 136.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.08 | 11.11 | 99% | 0.53 | 1.70 | 4.15 | 116.1 → 109.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 4.29 | 16.56 | 99% | 0.76 | 2.74 | 4.82 | 142.1 → 134.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.22 | 12.28 | 99% | 0.52 | 1.79 | 4.34 | 141.4 → 133.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.21 | 11.70 | 99% | 0.54 | 1.82 | 4.34 | 140.7 → 132.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.15 | 10.72 | 99% | 0.46 | 1.78 | 4.15 | 119.2 → 114.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.51 | 11.91 | 99% | 0.53 | 2.34 | 4.43 | 144.1 → 136.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 3.41 | 12.75 | 99% | 0.60 | 2.80 | 4.07 | 144.2 → 137.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.88 | 14.76 | 99% | 0.51 | 2.48 | 3.54 | 81.4 → 77.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 2.74 | 2.90 | 100% | 0.45 | 2.45 | 4.46 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 262 frames at 120 Hz; the text stood still on 0 of them, 252 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.94 | 12.40 | 99% | 0.51 | 2.56 | 4.41 | 139.8 → 137.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.17 | 5.51 | 19.65 | 98% | 0.74 | 4.49 | 4.75 | 351.4 → 346.2 | 9 | 1.1 / 4 |

