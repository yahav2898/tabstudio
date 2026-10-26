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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.00 | 11.00 | 100% | 0.32 | 1.73 | 4.05 | 112.7 → 113.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.71 | 13.16 | 99% | 0.61 | 2.05 | 4.41 | 150.3 → 148.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 4.46 | 14.74 | 99% | 1.23 | 2.65 | 4.92 | 186.2 → 169.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.13 | 15.17 | 99% | 0.47 | 1.77 | 4.12 | 130.2 → 127.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.92 | 9.86 | 99% | 0.47 | 1.62 | 3.92 | 107.1 → 100.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 2.10 | 14.59 | 99% | 0.50 | 1.71 | 4.10 | 132.4 → 124.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.25 | 18.29 | 99% | 0.79 | 2.49 | 4.62 | 132.6 → 125.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.60 | 2.06 | 13.26 | 99% | 0.49 | 1.72 | 4.04 | 131.7 → 124.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 1.99 | 8.25 | 99% | 0.42 | 1.70 | 3.91 | 110.1 → 105.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.08 | 11.72 | 99% | 0.48 | 1.72 | 4.10 | 135.0 → 127.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.49 | 13.36 | 99% | 0.58 | 1.97 | 3.87 | 135.0 → 127.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.32 | 14.37 | 99% | 0.48 | 1.68 | 3.31 | 71.8 → 68.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.165 |
| RaisingRoutedEvent RepeatButton | 0.037 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 1.84 | 10.44 | 98% | 0.49 | 1.59 | 4.33 | 78.1 → 98.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 26 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.14 | 11.48 | 99% | 0.48 | 1.78 | 4.15 | 130.8 → 128.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 4.89 | 79.58 | 98% | 1.23 | 4.37 | 4.61 | 343.1 → 336.4 | 9 | 1.1 / 4 |

