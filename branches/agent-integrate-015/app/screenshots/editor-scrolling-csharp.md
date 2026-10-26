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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.14 | 10.28 | 99% | 0.44 | 1.82 | 4.31 | 113.1 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.38 | 12.24 | 99% | 0.60 | 1.90 | 4.38 | 151.1 → 148.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 3.64 | 74.75 | 99% | 0.85 | 2.64 | 4.71 | 186.9 → 172.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 2.62 | 10.98 | 99% | 0.55 | 2.00 | 4.43 | 131.1 → 128.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.16 | 13.26 | 99% | 0.51 | 1.73 | 4.21 | 107.7 → 101.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.46 | 12.98 | 99% | 0.56 | 1.88 | 4.41 | 133.2 → 125.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.64 | 14.98 | 99% | 0.77 | 2.51 | 4.83 | 133.5 → 125.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.31 | 10.30 | 99% | 0.57 | 1.85 | 4.33 | 132.4 → 124.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.79 | 2.42 | 10.16 | 99% | 0.56 | 1.94 | 4.36 | 110.5 → 105.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.44 | 11.43 | 99% | 0.59 | 1.96 | 4.48 | 135.7 → 128.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.74 | 14.73 | 99% | 0.77 | 2.75 | 4.40 | 136.1 → 128.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.83 | 6.53 | 100% | 0.46 | 1.54 | 3.20 | 71.7 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.281 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 2.00 | 2.15 | 100% | 0.45 | 1.69 | 4.54 | 78.2 → 98.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 60 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.30 | 12.22 | 99% | 0.68 | 2.35 | 4.86 | 131.5 → 129.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.20 | 3.06 | 16.27 | 98% | 0.68 | 2.41 | 4.66 | 343.6 → 337.4 | 7 | 1.1 / 4 |

