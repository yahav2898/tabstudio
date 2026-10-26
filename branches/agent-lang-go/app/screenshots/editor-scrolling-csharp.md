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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 2.96 | 79.91 | 99% | 0.52 | 2.48 | 3.66 | 113.6 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.51 | 1.73 | 11.45 | 100% | 0.49 | 1.34 | 3.02 | 150.9 → 148.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.69 | 1.96 | 12.94 | 99% | 0.58 | 1.48 | 3.18 | 187.1 → 171.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 1.70 | 11.49 | 99% | 0.44 | 1.33 | 2.98 | 130.2 → 127.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.36 | 8.07 | 100% | 0.37 | 1.04 | 3.46 | 106.7 → 99.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.39 | 1.62 | 9.82 | 100% | 0.41 | 1.31 | 3.04 | 132.2 → 124.6 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.76 | 11.81 | 99% | 0.51 | 2.38 | 3.82 | 133.2 → 124.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.43 | 2.59 | 14.27 | 100% | 0.37 | 1.44 | 2.93 | 132.2 → 123.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.35 | 1.33 | 8.41 | 100% | 0.32 | 1.10 | 2.53 | 109.1 → 104.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 1.36 | 7.99 | 100% | 0.33 | 1.13 | 2.54 | 134.8 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 1.91 | 26.65 | 99% | 0.53 | 1.46 | 2.82 | 135.2 → 127.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.36 | 7.51 | 100% | 0.41 | 1.10 | 2.24 | 70.8 → 67.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.187 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.29 | 1.55 | 2.28 | 100% | 0.26 | 1.17 | 4.31 | 76.4 → 98.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 262 frames at 120 Hz; the text stood still on 0 of them, 18 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 1.87 | 8.27 | 100% | 0.51 | 1.50 | 3.10 | 131.3 → 128.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.89 | 2.15 | 13.77 | 98% | 0.47 | 1.67 | 3.20 | 344.7 → 337.8 | 7 | 1.1 / 4 |

