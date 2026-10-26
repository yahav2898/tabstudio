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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 1.26 | 1.43 | 100% | 0.27 | 1.00 | 2.91 | 112.5 → 113.9 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.40 | 37.71 | 100% | 0.41 | 1.05 | 3.00 | 151.0 → 148.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.62 | 1.48 | 32.19 | 100% | 0.51 | 1.06 | 3.06 | 187.2 → 171.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.30 | 1.60 | 100% | 0.37 | 1.02 | 2.98 | 130.5 → 127.6 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.40 | 38.49 | 100% | 0.36 | 0.98 | 2.81 | 106.9 → 100.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 2.75 | 3.75 | 100% | 0.57 | 2.34 | 3.01 | 133.2 → 124.7 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 1.29 | 21.96 | 100% | 0.36 | 1.02 | 2.98 | 132.8 → 124.8 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 1.28 | 33.67 | 100% | 0.38 | 1.00 | 2.90 | 132.0 → 123.9 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.21 | 1.48 | 100% | 0.31 | 0.97 | 2.81 | 109.2 → 104.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 1.29 | 33.86 | 100% | 0.38 | 1.02 | 2.99 | 135.4 → 127.6 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.50 | 1.49 | 3.70 | 100% | 0.39 | 1.10 | 2.66 | 135.0 → 127.3 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.32 | 6.17 | 100% | 0.40 | 0.88 | 2.19 | 71.1 → 67.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.140 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |
| MeasuringLayoutable Grid | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.40 | 1.25 | 1.32 | 100% | 0.26 | 0.95 | 3.08 | 76.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.50 | 1.30 | 1.59 | 100% | 0.37 | 1.02 | 2.96 | 131.4 → 128.1 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.76 | 1.58 | 36.71 | 99% | 0.42 | 1.23 | 3.00 | 345.6 → 337.7 | 2 | 1.1 / 4 |

