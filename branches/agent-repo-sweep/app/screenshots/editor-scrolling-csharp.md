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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.09 | 14.40 | 99% | 0.43 | 2.71 | 4.46 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 3.25 | 15.02 | 100% | 0.62 | 2.73 | 4.27 | 124.2 → 121.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 5.39 | 17.15 | 99% | 2.46 | 3.08 | 5.01 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.17 | 13.59 | 99% | 0.52 | 2.71 | 4.42 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.84 | 14.69 | 99% | 0.52 | 2.47 | 4.00 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 3.02 | 13.13 | 100% | 0.55 | 2.58 | 4.18 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 4.96 | 16.55 | 99% | 1.53 | 2.85 | 4.60 | 106.5 → 98.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 3.05 | 12.40 | 99% | 0.55 | 2.65 | 4.08 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.90 | 11.99 | 99% | 0.43 | 2.54 | 4.02 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 3.04 | 11.97 | 99% | 0.50 | 2.62 | 4.20 | 109.1 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 5.06 | 16.46 | 99% | 1.55 | 2.85 | 4.05 | 109.2 → 100.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.61 | 10.09 | 100% | 0.56 | 2.31 | 3.09 | 46.4 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.208 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 3.03 | 3.19 | 100% | 0.40 | 2.54 | 4.48 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 251 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 3.27 | 18.37 | 99% | 0.63 | 2.70 | 4.50 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 4.55 | 19.68 | 98% | 0.73 | 3.15 | 4.57 | 318.4 → 310.3 | 7 | 1.1 / 4 |

