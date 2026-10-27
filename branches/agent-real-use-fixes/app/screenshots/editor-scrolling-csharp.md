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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.00 | 2.63 | 10.70 | 99% | 0.50 | 2.19 | 10.52 | 133.2 → 133.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.16 | 2.86 | 13.52 | 99% | 0.70 | 2.23 | 10.52 | 171.2 → 168.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.67 | 5.02 | 14.59 | 99% | 1.35 | 4.21 | 11.27 | 207.8 → 194.9 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 2.73 | 11.89 | 99% | 0.64 | 2.18 | 10.16 | 150.5 → 147.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 2.35 | 10.53 | 99% | 0.54 | 1.87 | 9.64 | 126.9 → 120.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.17 | 4.65 | 16.61 | 99% | 0.73 | 4.16 | 11.14 | 153.1 → 148.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.14 | 2.79 | 12.45 | 99% | 0.65 | 2.23 | 10.54 | 153.2 → 145.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.09 | 2.72 | 13.25 | 99% | 0.64 | 2.19 | 10.27 | 152.2 → 143.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.21 | 4.35 | 12.15 | 99% | 0.72 | 2.92 | 10.68 | 130.5 → 126.9 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 2.75 | 14.40 | 99% | 0.63 | 2.23 | 10.71 | 155.8 → 148.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.12 | 2.76 | 14.25 | 99% | 0.65 | 2.21 | 9.21 | 154.9 → 147.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.90 | 2.85 | 8.04 | 100% | 0.68 | 2.11 | 8.86 | 91.8 → 88.2 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.300 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.19 | 2.86 | 11.83 | 98% | 0.57 | 2.14 | 11.10 | 101.4 → 122.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2251 ms over 202 frames at 120 Hz; the text stood still on 0 of them, 237 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.17 | 3.02 | 12.30 | 99% | 0.64 | 2.38 | 10.87 | 152.6 → 149.2 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.92 | 3.81 | 20.47 | 98% | 0.85 | 3.04 | 11.36 | 368.9 → 362.1 | 8 | 1.1 / 4 |

