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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.86 | 10.58 | 99% | 0.36 | 1.60 | 4.13 | 113.3 → 114.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 2.12 | 11.97 | 99% | 0.59 | 1.72 | 4.20 | 151.4 → 149.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 4.76 | 13.28 | 99% | 2.54 | 3.72 | 5.24 | 187.3 → 172.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.06 | 9.50 | 99% | 0.49 | 1.66 | 4.20 | 131.2 → 128.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.07 | 14.35 | 99% | 0.52 | 1.67 | 4.36 | 107.4 → 100.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.27 | 11.14 | 99% | 0.54 | 1.79 | 4.59 | 133.4 → 125.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 4.02 | 72.63 | 99% | 0.63 | 2.86 | 4.98 | 133.6 → 125.7 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.00 | 10.80 | 99% | 0.51 | 1.63 | 4.01 | 132.5 → 124.5 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.88 | 9.71 | 100% | 0.42 | 1.58 | 4.01 | 110.4 → 105.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.01 | 10.87 | 99% | 0.48 | 1.61 | 4.17 | 135.9 → 128.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 3.17 | 14.96 | 99% | 0.66 | 2.45 | 4.46 | 136.5 → 127.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.54 | 7.48 | 100% | 0.40 | 1.30 | 3.15 | 71.4 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.167 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.69 | 1.82 | 100% | 0.33 | 1.44 | 4.07 | 78.2 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 16 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.85 | 15.24 | 99% | 0.64 | 2.06 | 4.64 | 131.7 → 129.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.40 | 15.66 | 98% | 0.58 | 1.95 | 4.22 | 343.8 → 337.2 | 8 | 1.1 / 4 |

