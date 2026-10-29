# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.53 | 12.26 | 99% | 0.32 | 2.24 | 3.56 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 2.79 | 14.79 | 99% | 0.56 | 2.38 | 3.75 | 123.3 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 3.12 | 16.73 | 99% | 0.74 | 2.53 | 4.01 | 158.5 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.18 | 15.88 | 99% | 0.64 | 2.58 | 4.00 | 103.2 → 100.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.74 | 18.59 | 99% | 0.54 | 2.34 | 3.67 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 2.79 | 15.40 | 99% | 0.51 | 2.35 | 3.68 | 105.7 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.61 | 2.89 | 17.46 | 99% | 0.56 | 2.45 | 3.80 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.89 | 15.15 | 99% | 0.54 | 2.46 | 3.76 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.92 | 13.37 | 99% | 0.62 | 2.45 | 3.81 | 83.4 → 78.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.82 | 14.76 | 100% | 0.52 | 2.44 | 3.96 | 108.3 → 100.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 2.83 | 16.45 | 99% | 0.50 | 2.38 | 3.37 | 108.5 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.40 | 12.87 | 100% | 0.48 | 2.12 | 2.73 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 2.55 | 2.60 | 100% | 0.36 | 2.17 | 3.51 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 37 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.98 | 12.79 | 99% | 0.50 | 2.49 | 4.04 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.81 | 2.88 | 16.30 | 98% | 0.51 | 2.47 | 3.66 | 316.6 → 308.6 | 8 | 1.1 / 4 |

