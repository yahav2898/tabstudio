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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 3.19 | 13.93 | 100% | 0.45 | 2.80 | 4.49 | 86.6 → 87.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.64 | 14.54 | 99% | 0.74 | 2.97 | 4.87 | 124.5 → 122.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.25 | 5.37 | 18.03 | 99% | 2.13 | 3.15 | 5.22 | 160.3 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 3.58 | 17.26 | 99% | 0.67 | 2.94 | 4.89 | 104.1 → 101.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.21 | 9.32 | 100% | 0.65 | 2.74 | 4.55 | 81.0 → 73.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 3.68 | 14.38 | 99% | 0.80 | 3.01 | 4.94 | 106.7 → 98.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.58 | 14.91 | 99% | 0.71 | 2.96 | 4.98 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.39 | 16.32 | 99% | 0.63 | 2.84 | 4.58 | 105.9 → 97.6 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 3.37 | 13.48 | 99% | 0.59 | 2.82 | 4.55 | 83.8 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.01 | 4.17 | 14.84 | 99% | 1.60 | 3.06 | 5.29 | 109.3 → 101.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 3.40 | 16.13 | 99% | 0.65 | 2.88 | 4.29 | 109.3 → 101.0 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.96 | 10.81 | 100% | 0.66 | 2.48 | 3.57 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.241 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 3.27 | 3.37 | 100% | 0.45 | 2.66 | 5.00 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 188 frames at 120 Hz; the text stood still on 0 of them, 221 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.71 | 16.02 | 99% | 0.73 | 2.96 | 5.31 | 105.1 → 101.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 3.94 | 17.73 | 98% | 0.79 | 3.25 | 4.88 | 318.7 → 310.6 | 7 | 1.1 / 4 |

