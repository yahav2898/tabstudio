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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 2.22 | 8.31 | 100% | 0.44 | 1.84 | 4.48 | 113.2 → 114.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 2.84 | 15.56 | 99% | 0.67 | 2.17 | 4.90 | 151.2 → 149.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 4.53 | 16.12 | 99% | 1.21 | 2.81 | 5.10 | 188.0 → 171.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.32 | 13.23 | 99% | 0.56 | 1.84 | 4.52 | 130.8 → 128.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.14 | 10.89 | 99% | 0.56 | 1.71 | 4.26 | 107.9 → 101.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.89 | 2.94 | 13.85 | 99% | 0.72 | 2.19 | 4.96 | 133.6 → 126.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.91 | 3.06 | 12.20 | 99% | 0.62 | 2.43 | 4.84 | 133.5 → 125.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 2.39 | 11.64 | 99% | 0.58 | 1.84 | 4.45 | 132.4 → 124.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.14 | 11.10 | 99% | 0.50 | 1.74 | 4.19 | 110.5 → 105.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 4.55 | 14.44 | 99% | 0.81 | 2.51 | 5.01 | 136.0 → 129.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.23 | 11.38 | 99% | 0.56 | 1.81 | 4.06 | 136.0 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.18 | 10.43 | 99% | 0.46 | 2.00 | 3.23 | 72.4 → 68.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.189 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 2.60 | 3.21 | 100% | 0.42 | 2.33 | 4.83 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 255 frames at 120 Hz; the text stood still on 0 of them, 258 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 3.23 | 12.08 | 99% | 0.57 | 2.64 | 4.90 | 132.0 → 128.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.16 | 3.28 | 15.71 | 98% | 0.66 | 2.75 | 4.47 | 343.9 → 337.3 | 8 | 1.1 / 4 |

