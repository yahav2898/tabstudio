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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.58 | 9.36 | 99% | 0.30 | 1.32 | 3.32 | 112.7 → 114.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 3.09 | 12.36 | 99% | 0.58 | 2.49 | 3.88 | 152.1 → 149.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 1.66 | 14.01 | 99% | 0.52 | 1.27 | 3.28 | 187.4 → 171.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.46 | 1.50 | 9.86 | 99% | 0.39 | 1.22 | 3.22 | 130.8 → 127.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 1.35 | 9.06 | 100% | 0.38 | 1.10 | 3.04 | 107.2 → 100.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.45 | 1.47 | 12.23 | 99% | 0.40 | 1.20 | 3.17 | 133.2 → 125.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 2.73 | 13.83 | 99% | 0.50 | 2.26 | 3.57 | 133.4 → 125.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.52 | 2.74 | 11.45 | 99% | 0.42 | 2.35 | 3.65 | 132.8 → 124.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.42 | 1.40 | 9.56 | 99% | 0.33 | 1.16 | 3.01 | 109.9 → 104.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.48 | 1.86 | 10.60 | 99% | 0.39 | 1.66 | 3.21 | 135.7 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.48 | 1.94 | 10.19 | 99% | 0.39 | 1.68 | 2.92 | 135.7 → 127.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.74 | 5.86 | 100% | 0.35 | 1.53 | 2.50 | 71.1 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.292 |
| ArrangingLayoutable NumberDrawing | 0.040 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable LineDrawing | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.77 | 2.88 | 4.47 | 100% | 0.52 | 2.29 | 4.46 | 78.7 → 100.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.48 | 2.00 | 10.43 | 99% | 0.40 | 1.71 | 3.23 | 131.4 → 128.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.79 | 2.25 | 14.83 | 98% | 0.47 | 1.93 | 3.32 | 345.5 → 338.6 | 8 | 1.1 / 4 |

