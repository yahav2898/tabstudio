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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.91 | 13.42 | 100% | 0.40 | 2.59 | 4.24 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 3.18 | 17.33 | 99% | 0.63 | 2.71 | 4.29 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 5.27 | 31.00 | 99% | 1.74 | 3.06 | 4.93 | 158.1 → 143.2 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 3.07 | 20.57 | 99% | 0.53 | 2.61 | 4.30 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.83 | 16.35 | 99% | 0.57 | 2.43 | 4.05 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 2.95 | 16.93 | 99% | 0.56 | 2.53 | 4.18 | 105.1 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 3.00 | 15.44 | 99% | 0.58 | 2.58 | 4.19 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.94 | 14.86 | 99% | 2.21 | 2.79 | 4.65 | 104.3 → 96.0 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.58 | 2.89 | 14.49 | 99% | 0.46 | 2.50 | 4.00 | 82.6 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.62 | 2.99 | 14.73 | 99% | 0.58 | 2.55 | 4.26 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 3.00 | 16.11 | 99% | 0.55 | 2.59 | 3.72 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.63 | 11.56 | 100% | 0.54 | 2.30 | 3.07 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.302 |
| AttachingStyle - | 0.079 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable TextView | 0.017 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.014 |
| MeasuringLayoutable NumberDrawing | 0.011 |
| EvaluatingStyle - | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.49 | 2.90 | 2.99 | 100% | 0.39 | 2.46 | 4.06 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 246 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 3.10 | 11.98 | 100% | 0.55 | 2.63 | 4.25 | 103.3 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 4.14 | 19.48 | 98% | 0.75 | 3.10 | 4.50 | 308.8 → 301.0 | 7 | 1.1 / 4 |

