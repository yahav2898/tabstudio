# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.35 | 15.84 | 99% | 0.37 | 2.02 | 2.76 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.55 | 15.59 | 100% | 0.49 | 2.10 | 2.85 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 2.59 | 17.01 | 99% | 0.69 | 2.08 | 3.03 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.67 | 15.71 | 100% | 0.52 | 2.23 | 2.94 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.84 | 107.42 | 99% | 1.40 | 2.11 | 3.23 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.55 | 2.35 | 15.15 | 99% | 0.45 | 1.97 | 2.80 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.52 | 2.38 | 17.14 | 99% | 0.47 | 2.00 | 2.93 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.39 | 14.31 | 99% | 0.46 | 1.99 | 2.91 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.49 | 2.30 | 13.08 | 99% | 0.41 | 1.97 | 2.71 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.54 | 2.35 | 14.41 | 99% | 0.44 | 1.99 | 2.82 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.48 | 2.31 | 13.89 | 99% | 0.43 | 1.98 | 2.42 | 107.6 → 99.3 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.12 | 20.34 | 100% | 1.11 | 1.78 | 1.98 | 46.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.270 |
| AttachingStyle - | 0.070 |
| ArrangingLayoutable CachedLineNumberMargin | 0.029 |
| FindingResource - | 0.023 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| MeasuringLayoutable Track | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 2.61 | 41.08 | 98% | 0.41 | 1.96 | 3.09 | 49.2 → 73.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 121 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.55 | 2.41 | 14.00 | 99% | 0.54 | 2.01 | 2.97 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.86 | 2.95 | 21.14 | 98% | 0.60 | 2.35 | 3.35 | 308.8 → 301.0 | 7 | 1.1 / 4 |

