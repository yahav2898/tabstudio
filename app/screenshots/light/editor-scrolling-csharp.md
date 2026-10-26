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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.83 | 3.24 | 13.99 | 99% | 0.50 | 2.54 | 4.21 | 113.2 → 114.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.59 | 14.85 | 99% | 0.62 | 1.96 | 4.09 | 150.2 → 148.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.59 | 13.52 | 99% | 0.71 | 1.91 | 4.16 | 185.5 → 169.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.33 | 10.96 | 99% | 0.54 | 1.87 | 4.07 | 130.2 → 127.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.07 | 10.47 | 100% | 0.52 | 1.68 | 3.71 | 106.4 → 99.7 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.95 | 10.71 | 100% | 0.62 | 2.41 | 4.12 | 132.4 → 124.3 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 2.09 | 15.16 | 100% | 0.50 | 1.67 | 3.84 | 131.8 → 124.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.08 | 10.78 | 99% | 0.50 | 1.66 | 3.83 | 131.1 → 123.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 2.28 | 10.81 | 99% | 0.53 | 1.76 | 3.78 | 109.5 → 104.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.89 | 2.62 | 14.26 | 99% | 0.65 | 1.96 | 4.18 | 134.8 → 127.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 3.16 | 14.01 | 99% | 0.65 | 2.41 | 3.75 | 135.1 → 127.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.02 | 8.92 | 100% | 0.53 | 1.59 | 3.16 | 71.3 → 68.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.251 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 2.30 | 2.39 | 100% | 0.51 | 1.77 | 4.15 | 78.1 → 98.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 13 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.83 | 12.26 | 99% | 0.55 | 1.88 | 3.87 | 130.6 → 128.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.07 | 3.05 | 17.21 | 98% | 0.63 | 2.13 | 4.00 | 341.7 → 335.2 | 7 | 1.1 / 4 |

