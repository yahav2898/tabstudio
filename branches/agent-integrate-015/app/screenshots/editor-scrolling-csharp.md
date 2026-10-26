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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 2.55 | 13.09 | 99% | 0.51 | 2.14 | 4.91 | 117.8 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 4.18 | 78.14 | 99% | 0.83 | 2.76 | 5.40 | 156.8 → 154.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 2.99 | 16.24 | 99% | 0.85 | 2.24 | 5.14 | 192.5 → 176.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 2.75 | 14.49 | 99% | 0.63 | 2.18 | 5.09 | 135.8 → 133.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 2.50 | 12.05 | 99% | 0.61 | 1.99 | 4.85 | 112.5 → 105.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 3.90 | 13.31 | 99% | 0.95 | 2.71 | 5.70 | 138.9 → 130.8 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 2.70 | 13.64 | 99% | 0.67 | 2.20 | 5.00 | 137.9 → 130.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 2.83 | 12.92 | 99% | 0.68 | 2.17 | 4.95 | 137.4 → 129.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 2.68 | 10.77 | 99% | 0.59 | 2.12 | 4.88 | 115.7 → 110.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 3.58 | 17.78 | 99% | 0.77 | 2.63 | 5.42 | 142.0 → 133.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.04 | 2.66 | 12.89 | 99% | 0.68 | 2.14 | 4.41 | 140.5 → 132.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 2.27 | 7.65 | 100% | 0.55 | 1.76 | 3.72 | 77.1 → 73.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.238 |
| ArrangingLayoutable TextView | 0.054 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 2.52 | 3.07 | 100% | 0.54 | 2.12 | 5.19 | 82.8 → 104.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 215 frames at 120 Hz; the text stood still on 0 of them, 243 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.98 | 2.86 | 11.70 | 99% | 0.65 | 2.21 | 5.06 | 136.6 → 133.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.47 | 3.28 | 18.66 | 98% | 0.77 | 2.55 | 5.10 | 350.1 → 343.4 | 7 | 1.1 / 4 |

