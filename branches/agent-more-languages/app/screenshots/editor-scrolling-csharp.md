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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 3.43 | 15.82 | 99% | 0.52 | 2.96 | 5.04 | 86.5 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 3.88 | 19.10 | 99% | 1.54 | 3.13 | 5.27 | 124.2 → 122.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 3.92 | 19.94 | 99% | 0.97 | 3.05 | 5.26 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 3.56 | 18.46 | 100% | 0.69 | 2.96 | 5.11 | 104.0 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 3.43 | 14.16 | 99% | 0.65 | 2.83 | 4.87 | 81.4 → 74.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.03 | 3.84 | 16.31 | 99% | 0.78 | 3.07 | 5.25 | 106.6 → 98.2 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 3.54 | 12.11 | 100% | 0.67 | 2.96 | 5.03 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 3.65 | 19.60 | 99% | 0.77 | 2.98 | 5.06 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 3.56 | 14.93 | 100% | 0.66 | 2.96 | 5.01 | 83.9 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.05 | 3.77 | 18.79 | 99% | 0.76 | 3.06 | 5.27 | 109.2 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.61 | 17.47 | 99% | 0.70 | 2.99 | 4.56 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.16 | 11.66 | 100% | 1.23 | 2.63 | 3.85 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| AttachingStyle - | 0.066 |
| ArrangingLayoutable TextView | 0.045 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| FindingResource - | 0.015 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 3.64 | 3.77 | 100% | 0.62 | 2.90 | 5.34 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 171 frames at 120 Hz; the text stood still on 0 of them, 206 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 3.78 | 19.38 | 99% | 0.75 | 3.07 | 5.26 | 104.9 → 100.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.50 | 4.26 | 20.68 | 98% | 0.84 | 3.43 | 5.35 | 318.4 → 310.4 | 7 | 1.1 / 4 |

