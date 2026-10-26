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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.21 | 11.64 | 99% | 0.41 | 1.84 | 3.79 | 117.6 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 4.23 | 17.24 | 99% | 0.69 | 2.59 | 3.93 | 155.7 → 153.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 3.06 | 15.89 | 99% | 0.66 | 2.45 | 4.18 | 191.3 → 175.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.95 | 12.75 | 99% | 0.51 | 2.59 | 4.15 | 135.4 → 132.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.15 | 11.37 | 99% | 0.43 | 1.96 | 3.44 | 111.8 → 105.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.89 | 16.56 | 99% | 0.57 | 2.44 | 4.16 | 137.4 → 129.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.00 | 15.44 | 99% | 0.53 | 2.48 | 3.84 | 137.8 → 129.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 2.86 | 10.45 | 100% | 0.50 | 2.50 | 3.70 | 137.2 → 128.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.26 | 11.82 | 99% | 0.45 | 1.99 | 3.49 | 114.7 → 109.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.31 | 13.16 | 99% | 0.54 | 1.85 | 4.09 | 140.2 → 132.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.57 | 9.62 | 99% | 0.53 | 2.16 | 3.45 | 140.1 → 132.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.24 | 9.01 | 100% | 0.42 | 1.99 | 2.68 | 76.0 → 72.5 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.257 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable NumberDrawing | 0.045 |
| ArrangingLayoutable TextView | 0.017 |
| ArrangingLayoutable Track | 0.017 |
| FindingResource - | 0.012 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.69 | 3.76 | 8.18 | 98% | 0.44 | 3.56 | 3.98 | 82.9 → 104.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 14 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.60 | 12.19 | 99% | 0.48 | 2.25 | 3.79 | 136.1 → 133.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 3.04 | 17.26 | 98% | 0.56 | 2.61 | 3.74 | 348.2 → 341.5 | 7 | 1.1 / 4 |

