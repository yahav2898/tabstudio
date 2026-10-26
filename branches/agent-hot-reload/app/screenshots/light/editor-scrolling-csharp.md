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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.84 | 15.69 | 99% | 0.41 | 2.49 | 4.55 | 112.7 → 114.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 3.10 | 13.94 | 99% | 0.66 | 2.65 | 4.77 | 150.7 → 148.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.24 | 5.19 | 16.86 | 99% | 2.46 | 4.17 | 5.05 | 186.4 → 171.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.08 | 11.61 | 99% | 0.58 | 2.65 | 4.54 | 130.3 → 127.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.77 | 10.89 | 99% | 0.54 | 2.37 | 4.17 | 107.2 → 100.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.36 | 13.90 | 99% | 0.55 | 1.94 | 4.30 | 132.4 → 124.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 3.28 | 12.95 | 99% | 0.63 | 2.73 | 4.77 | 132.8 → 125.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.46 | 13.30 | 99% | 0.51 | 2.01 | 4.32 | 131.6 → 124.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.56 | 9.41 | 100% | 0.44 | 2.28 | 3.99 | 110.0 → 105.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 2.97 | 13.12 | 99% | 0.58 | 2.52 | 4.45 | 134.9 → 127.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.48 | 12.75 | 99% | 0.57 | 2.73 | 4.31 | 135.6 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.22 | 8.94 | 100% | 0.42 | 2.04 | 3.11 | 71.7 → 68.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.168 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 2.58 | 2.63 | 100% | 0.34 | 2.26 | 4.24 | 78.0 → 98.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 94 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 4.52 | 74.41 | 99% | 0.65 | 2.76 | 4.42 | 131.0 → 128.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 3.49 | 15.96 | 98% | 0.65 | 2.92 | 4.53 | 342.4 → 335.8 | 8 | 1.1 / 4 |

