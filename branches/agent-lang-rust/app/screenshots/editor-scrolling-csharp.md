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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.44 | 11.34 | 99% | 0.42 | 2.16 | 4.60 | 110.9 → 112.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.52 | 13.04 | 99% | 0.76 | 2.65 | 4.91 | 149.8 → 146.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.00 | 16.05 | 99% | 0.74 | 2.43 | 4.68 | 184.6 → 168.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.69 | 13.60 | 99% | 0.55 | 2.37 | 4.41 | 128.7 → 125.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 3.01 | 12.24 | 100% | 0.79 | 2.43 | 4.71 | 105.7 → 99.3 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.90 | 12.34 | 99% | 0.54 | 2.40 | 4.54 | 130.9 → 123.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.77 | 13.03 | 99% | 0.58 | 2.37 | 4.48 | 130.9 → 123.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.45 | 13.15 | 99% | 0.69 | 2.61 | 4.90 | 130.3 → 123.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 3.29 | 77.83 | 99% | 0.57 | 2.67 | 5.24 | 109.2 → 103.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.08 | 13.83 | 99% | 0.64 | 2.61 | 4.78 | 133.6 → 126.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 3.13 | 14.60 | 99% | 0.66 | 2.58 | 4.47 | 133.6 → 126.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 2.73 | 7.13 | 100% | 0.54 | 2.30 | 3.94 | 70.7 → 67.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.285 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 2.30 | 2.54 | 100% | 0.38 | 1.93 | 4.47 | 75.9 → 97.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 180 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 3.43 | 15.03 | 99% | 0.69 | 2.78 | 4.98 | 129.4 → 127.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.29 | 3.50 | 18.67 | 98% | 0.68 | 2.94 | 4.76 | 342.2 → 335.5 | 7 | 1.1 / 4 |

