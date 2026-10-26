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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.26 | 11.46 | 99% | 0.50 | 1.80 | 3.52 | 113.6 → 115.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 3.32 | 15.03 | 99% | 0.74 | 2.61 | 3.67 | 152.1 → 150.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 2.61 | 14.16 | 99% | 0.80 | 1.79 | 3.59 | 188.4 → 172.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.99 | 2.39 | 12.46 | 99% | 0.64 | 1.75 | 3.60 | 131.6 → 128.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.15 | 11.23 | 99% | 0.59 | 1.59 | 3.37 | 108.0 → 101.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 2.34 | 14.18 | 99% | 0.62 | 1.75 | 3.50 | 133.9 → 125.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.14 | 14.44 | 99% | 0.74 | 1.88 | 3.59 | 134.1 → 125.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.98 | 2.68 | 68.65 | 99% | 0.66 | 1.89 | 3.59 | 133.5 → 124.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 2.27 | 10.94 | 99% | 0.57 | 1.70 | 3.40 | 110.5 → 105.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 2.34 | 10.97 | 99% | 0.61 | 1.79 | 3.45 | 136.4 → 128.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 2.36 | 14.98 | 99% | 0.60 | 1.77 | 3.30 | 136.2 → 128.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 1.83 | 8.76 | 100% | 0.52 | 1.39 | 2.62 | 71.5 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.352 |
| ArrangingLayoutable ScrollingTextLayer | 0.064 |
| ArrangingLayoutable TextView | 0.027 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable Track | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| MeasuringLayoutable NumberDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.90 | 3.08 | 3.15 | 100% | 0.53 | 1.75 | 3.65 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 2.45 | 14.20 | 99% | 0.63 | 1.83 | 3.62 | 132.4 → 129.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 2.82 | 17.29 | 98% | 0.72 | 2.08 | 3.67 | 345.6 → 339.0 | 7 | 1.1 / 4 |

