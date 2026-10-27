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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 3.38 | 10.92 | 99% | 0.49 | 2.68 | 4.82 | 123.5 → 123.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.30 | 16.12 | 99% | 0.54 | 1.83 | 4.26 | 160.1 → 157.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 2.43 | 15.06 | 99% | 0.67 | 1.89 | 4.32 | 195.8 → 180.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.20 | 12.39 | 99% | 0.50 | 1.81 | 4.21 | 139.8 → 137.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.01 | 11.20 | 99% | 0.49 | 1.69 | 3.98 | 116.8 → 110.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 4.30 | 83.16 | 99% | 0.68 | 3.91 | 4.57 | 142.3 → 134.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 3.04 | 13.45 | 99% | 0.51 | 2.63 | 4.24 | 142.4 → 134.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.24 | 12.46 | 99% | 0.53 | 1.93 | 4.12 | 141.4 → 133.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.24 | 10.79 | 99% | 0.45 | 1.88 | 4.04 | 119.4 → 114.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 2.36 | 13.48 | 99% | 0.52 | 1.97 | 4.33 | 144.8 → 137.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 4.74 | 85.71 | 99% | 2.44 | 4.04 | 4.45 | 145.1 → 137.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.31 | 10.15 | 100% | 0.44 | 1.78 | 3.11 | 81.3 → 77.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 2.71 | 2.80 | 100% | 0.35 | 2.46 | 4.33 | 87.3 → 109.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 267 frames at 120 Hz; the text stood still on 0 of them, 242 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.54 | 12.63 | 99% | 0.50 | 2.02 | 4.20 | 140.4 → 137.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.71 | 5.58 | 19.63 | 98% | 2.56 | 5.09 | 4.92 | 353.8 → 348.7 | 9 | 1.1 / 4 |

