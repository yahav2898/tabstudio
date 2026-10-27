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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 3.18 | 13.16 | 99% | 0.53 | 2.57 | 5.07 | 121.6 → 122.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.59 | 13.25 | 99% | 0.64 | 2.06 | 4.71 | 159.3 → 157.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 2.87 | 14.46 | 99% | 0.84 | 2.14 | 5.01 | 195.4 → 180.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 2.89 | 14.23 | 99% | 0.69 | 2.24 | 5.26 | 139.0 → 136.7 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.43 | 14.01 | 99% | 0.59 | 2.48 | 4.79 | 115.4 → 108.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.33 | 12.26 | 99% | 0.58 | 1.86 | 4.66 | 141.0 → 133.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.50 | 16.85 | 99% | 0.59 | 2.00 | 4.87 | 141.0 → 133.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.20 | 12.19 | 99% | 0.55 | 1.84 | 4.51 | 140.1 → 132.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.37 | 17.27 | 99% | 0.68 | 2.63 | 5.10 | 118.5 → 114.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.56 | 11.26 | 99% | 0.59 | 2.01 | 4.77 | 143.6 → 136.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 2.76 | 13.87 | 99% | 0.63 | 2.16 | 4.41 | 143.7 → 135.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 2.40 | 9.48 | 100% | 0.66 | 1.87 | 3.86 | 79.3 → 75.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.311 |
| AttachingStyle - | 0.051 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.98 | 4.46 | 4.95 | 100% | 0.52 | 4.08 | 5.47 | 86.5 → 108.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 207 frames at 120 Hz; the text stood still on 0 of them, 240 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 2.98 | 14.44 | 99% | 0.73 | 2.31 | 5.12 | 140.2 → 137.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.65 | 3.52 | 19.68 | 98% | 0.89 | 2.69 | 5.26 | 354.8 → 348.2 | 8 | 1.1 / 4 |

