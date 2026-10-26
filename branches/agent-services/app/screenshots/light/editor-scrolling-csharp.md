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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.34 | 12.89 | 99% | 0.52 | 1.86 | 4.55 | 115.6 → 117.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 2.49 | 13.11 | 99% | 0.67 | 1.87 | 4.49 | 153.6 → 151.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.13 | 2.70 | 15.47 | 99% | 0.79 | 1.92 | 4.68 | 189.3 → 173.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 4.26 | 17.70 | 99% | 0.69 | 2.80 | 4.72 | 133.6 → 131.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.24 | 11.00 | 99% | 0.59 | 1.71 | 4.31 | 109.9 → 103.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.44 | 15.30 | 99% | 0.67 | 1.84 | 4.59 | 135.6 → 127.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 2.49 | 15.86 | 99% | 0.71 | 1.91 | 4.57 | 135.6 → 127.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.93 | 2.48 | 11.77 | 99% | 0.64 | 1.84 | 4.40 | 134.8 → 127.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.94 | 3.07 | 13.80 | 99% | 0.60 | 2.56 | 4.33 | 113.2 → 107.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 2.41 | 12.31 | 99% | 0.64 | 1.86 | 4.56 | 138.1 → 130.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 2.52 | 16.14 | 99% | 0.66 | 1.88 | 4.26 | 138.3 → 130.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.70 | 2.18 | 8.62 | 100% | 0.59 | 1.59 | 3.77 | 74.3 → 70.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.274 |
| ArrangingLayoutable CachedLineNumberMargin | 0.052 |
| ArrangingLayoutable TextView | 0.034 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable Track | 0.019 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 3.20 | 4.26 | 100% | 0.50 | 2.57 | 4.63 | 80.6 → 102.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 256 frames at 120 Hz; the text stood still on 0 of them, 247 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 2.49 | 13.51 | 99% | 0.67 | 1.90 | 4.64 | 134.1 → 131.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 2.87 | 17.69 | 98% | 0.73 | 2.15 | 4.57 | 346.2 → 340.0 | 7 | 1.1 / 4 |

