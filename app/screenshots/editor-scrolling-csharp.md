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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.66 | 8.96 | 100% | 0.35 | 2.35 | 4.15 | 117.6 → 119.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.97 | 14.19 | 99% | 0.60 | 2.50 | 4.46 | 155.8 → 153.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 4.77 | 16.26 | 99% | 2.48 | 2.81 | 4.69 | 192.7 → 175.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.87 | 12.67 | 99% | 0.52 | 2.51 | 4.40 | 135.5 → 132.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.56 | 9.11 | 99% | 0.50 | 2.27 | 4.04 | 112.4 → 105.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.66 | 8.74 | 99% | 0.49 | 2.38 | 4.14 | 137.7 → 129.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 4.17 | 73.10 | 99% | 0.59 | 3.76 | 4.75 | 138.3 → 130.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.14 | 10.83 | 99% | 0.50 | 1.73 | 4.10 | 136.9 → 129.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.00 | 10.53 | 99% | 0.42 | 1.67 | 4.03 | 115.0 → 110.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.13 | 11.04 | 99% | 0.50 | 1.75 | 4.26 | 140.2 → 132.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 3.01 | 17.78 | 99% | 0.63 | 2.49 | 4.16 | 140.2 → 132.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.15 | 6.28 | 100% | 0.43 | 1.63 | 3.24 | 76.5 → 72.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.178 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 1.83 | 2.21 | 100% | 0.35 | 1.61 | 5.28 | 82.8 → 103.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 36 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.10 | 10.84 | 99% | 0.49 | 1.70 | 4.16 | 136.0 → 133.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.25 | 4.93 | 17.29 | 98% | 0.69 | 4.41 | 4.73 | 350.3 → 341.6 | 8 | 1.1 / 4 |

