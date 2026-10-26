# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.61 | 7.94 | 100% | 0.34 | 1.29 | 2.76 | 111.8 → 113.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 1.62 | 9.93 | 99% | 0.44 | 1.27 | 2.73 | 150.0 → 147.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.65 | 1.75 | 12.75 | 99% | 0.56 | 1.34 | 2.88 | 185.9 → 169.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 1.56 | 10.47 | 99% | 0.42 | 1.28 | 2.80 | 129.7 → 126.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.20 | 62.85 | 99% | 0.58 | 2.29 | 3.00 | 106.6 → 100.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 1.62 | 10.14 | 99% | 0.44 | 1.30 | 2.81 | 132.0 → 124.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 1.64 | 13.33 | 99% | 0.46 | 1.28 | 2.80 | 131.9 → 124.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 1.64 | 9.70 | 99% | 0.43 | 1.32 | 2.80 | 131.6 → 123.1 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.48 | 1.55 | 8.74 | 100% | 0.37 | 1.23 | 2.60 | 108.8 → 104.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.74 | 10.05 | 99% | 0.49 | 1.35 | 3.26 | 134.7 → 126.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 3.06 | 15.74 | 99% | 0.70 | 2.45 | 3.03 | 134.8 → 127.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.22 | 7.77 | 100% | 0.37 | 1.03 | 2.03 | 70.1 → 66.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.221 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 1.67 | 1.83 | 100% | 0.37 | 1.31 | 2.98 | 76.3 → 98.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 21 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.20 | 12.23 | 99% | 0.57 | 1.65 | 3.38 | 131.0 → 128.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.23 | 4.58 | 22.42 | 98% | 2.35 | 3.90 | 3.70 | 343.6 → 337.4 | 9 | 1.1 / 4 |

