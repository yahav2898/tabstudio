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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 2.32 | 11.69 | 99% | 0.49 | 1.87 | 4.26 | 113.4 → 114.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.56 | 12.78 | 99% | 0.67 | 1.98 | 4.38 | 151.5 → 149.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 2.62 | 12.81 | 100% | 0.77 | 1.94 | 4.35 | 187.1 → 171.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 3.83 | 12.78 | 99% | 0.72 | 2.73 | 4.37 | 131.3 → 128.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.18 | 9.70 | 100% | 0.56 | 1.69 | 3.90 | 107.6 → 100.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.33 | 16.47 | 99% | 0.58 | 1.80 | 4.13 | 133.7 → 125.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.36 | 13.38 | 99% | 0.60 | 1.82 | 4.16 | 133.3 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 2.50 | 12.85 | 99% | 0.63 | 1.82 | 4.06 | 132.6 → 124.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.01 | 10.70 | 99% | 0.55 | 2.33 | 4.03 | 110.9 → 105.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.26 | 12.42 | 99% | 0.56 | 1.78 | 4.09 | 135.8 → 128.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.19 | 11.97 | 99% | 0.54 | 1.73 | 3.59 | 135.9 → 127.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 1.81 | 8.99 | 100% | 0.48 | 1.46 | 2.98 | 71.4 → 67.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.214 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.78 | 2.40 | 3.17 | 100% | 0.47 | 1.82 | 4.35 | 78.3 → 100.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 141 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.25 | 9.34 | 99% | 0.56 | 1.81 | 4.15 | 131.7 → 129.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 2.56 | 14.14 | 99% | 0.64 | 2.03 | 4.08 | 343.7 → 337.2 | 7 | 1.1 / 4 |

