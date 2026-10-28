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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.51 | 10.93 | 99% | 0.42 | 2.09 | 2.82 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.85 | 13.91 | 99% | 1.33 | 2.23 | 3.04 | 124.2 → 121.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 3.91 | 84.90 | 99% | 2.55 | 2.44 | 3.37 | 159.9 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.61 | 16.83 | 99% | 0.50 | 2.16 | 2.94 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.46 | 11.67 | 99% | 0.52 | 2.00 | 2.68 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.44 | 12.27 | 99% | 0.48 | 2.03 | 2.81 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.47 | 13.47 | 99% | 0.51 | 2.07 | 2.84 | 106.4 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.89 | 14.98 | 100% | 0.58 | 2.26 | 2.93 | 105.7 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.89 | 11.17 | 99% | 0.47 | 2.44 | 3.12 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.54 | 11.12 | 99% | 0.49 | 2.12 | 2.87 | 109.0 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.50 | 16.10 | 99% | 0.55 | 2.07 | 2.65 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.22 | 3.65 | 100% | 0.48 | 1.85 | 1.96 | 46.5 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.69 | 2.61 | 2.63 | 100% | 0.43 | 2.08 | 3.03 | 50.3 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 24 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.03 | 13.29 | 100% | 0.60 | 2.33 | 3.32 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 2.99 | 15.58 | 98% | 0.58 | 2.40 | 3.02 | 318.3 → 310.2 | 7 | 1.1 / 4 |

