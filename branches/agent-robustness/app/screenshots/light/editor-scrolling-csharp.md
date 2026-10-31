# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.70 | 13.20 | 99% | 0.41 | 2.34 | 3.70 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 3.05 | 15.14 | 99% | 0.66 | 2.49 | 4.07 | 121.2 → 121.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 5.20 | 19.51 | 99% | 2.65 | 2.74 | 4.52 | 156.7 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.96 | 18.38 | 99% | 0.59 | 2.45 | 4.03 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.59 | 13.24 | 99% | 0.51 | 2.21 | 3.54 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 2.94 | 14.67 | 100% | 0.59 | 2.41 | 4.01 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.12 | 14.97 | 99% | 0.63 | 2.56 | 4.10 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.76 | 3.00 | 21.87 | 99% | 0.63 | 2.45 | 3.96 | 103.5 → 95.3 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.81 | 3.17 | 35.15 | 99% | 0.73 | 2.49 | 4.08 | 82.3 → 77.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.79 | 3.04 | 15.55 | 99% | 0.67 | 2.49 | 4.21 | 106.6 → 98.7 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.59 | 2.63 | 18.21 | 99% | 0.51 | 2.23 | 3.20 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.34 | 13.22 | 100% | 0.49 | 2.04 | 2.69 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 2.68 | 2.79 | 100% | 0.42 | 2.20 | 3.84 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 249 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 3.16 | 17.80 | 99% | 0.65 | 2.52 | 4.48 | 102.6 → 99.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.16 | 3.54 | 22.46 | 98% | 0.75 | 2.78 | 4.18 | 307.6 → 299.8 | 7 | 1.1 / 4 |

