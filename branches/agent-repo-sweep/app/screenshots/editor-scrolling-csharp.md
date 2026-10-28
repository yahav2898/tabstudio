# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.07 | 17.05 | 100% | 0.44 | 1.69 | 4.41 | 86.4 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.40 | 15.19 | 99% | 0.73 | 1.82 | 4.61 | 124.2 → 121.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 4.52 | 95.43 | 99% | 2.76 | 2.57 | 4.96 | 160.0 → 144.2 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.20 | 14.79 | 99% | 0.58 | 1.71 | 4.47 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.93 | 12.91 | 99% | 0.58 | 1.51 | 4.24 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.10 | 16.71 | 99% | 0.59 | 1.65 | 4.36 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.85 | 16.11 | 99% | 0.77 | 1.89 | 4.70 | 106.4 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.29 | 16.02 | 99% | 0.62 | 1.76 | 4.48 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.16 | 13.23 | 99% | 0.53 | 1.69 | 4.30 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.24 | 14.67 | 99% | 0.60 | 1.74 | 4.46 | 109.0 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.20 | 14.66 | 99% | 0.60 | 1.73 | 3.91 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.60 | 18.94 | 100% | 1.42 | 1.50 | 3.48 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 2.02 | 10.47 | 98% | 0.41 | 1.58 | 4.67 | 50.2 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 142 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.15 | 15.86 | 99% | 0.60 | 1.69 | 4.49 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 4.81 | 83.35 | 98% | 1.66 | 4.21 | 4.85 | 318.3 → 310.4 | 8 | 1.1 / 4 |

