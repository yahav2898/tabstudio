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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.70 | 103.72 | 99% | 0.40 | 2.31 | 3.58 | 86.5 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 2.53 | 13.43 | 99% | 0.49 | 2.16 | 3.34 | 124.2 → 121.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 2.69 | 14.96 | 99% | 0.65 | 2.23 | 3.69 | 159.9 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.59 | 14.20 | 99% | 0.47 | 2.26 | 3.47 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.47 | 14.11 | 99% | 0.49 | 2.13 | 3.33 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.85 | 75.48 | 99% | 1.41 | 2.46 | 3.65 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.50 | 2.50 | 11.47 | 99% | 0.44 | 2.18 | 3.37 | 106.4 → 98.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.48 | 2.43 | 15.54 | 99% | 0.44 | 2.16 | 3.30 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 2.56 | 12.28 | 99% | 0.38 | 2.25 | 3.34 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 2.56 | 13.31 | 99% | 0.46 | 2.27 | 3.56 | 109.0 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 3.09 | 15.04 | 99% | 1.42 | 2.40 | 3.51 | 109.2 → 100.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.38 | 6.08 | 100% | 0.81 | 2.07 | 2.56 | 46.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.151 |
| AttachingStyle - | 0.044 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.40 | 2.27 | 2.42 | 100% | 0.31 | 2.08 | 3.29 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 19 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.48 | 2.47 | 13.64 | 99% | 0.42 | 2.16 | 3.36 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.86 | 3.87 | 14.72 | 98% | 0.57 | 2.67 | 3.61 | 318.3 → 310.3 | 8 | 1.1 / 4 |

