# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.37 | 12.13 | 99% | 0.45 | 2.00 | 4.57 | 113.8 → 115.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 2.62 | 14.62 | 99% | 0.64 | 2.11 | 4.67 | 152.4 → 150.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 4.87 | 86.34 | 99% | 2.60 | 3.98 | 5.00 | 189.3 → 173.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.58 | 13.85 | 99% | 0.62 | 2.05 | 4.79 | 131.9 → 129.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 2.37 | 10.92 | 100% | 0.59 | 1.92 | 4.46 | 108.4 → 101.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.52 | 12.42 | 99% | 0.63 | 2.03 | 4.70 | 134.1 → 126.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 3.77 | 88.27 | 99% | 0.77 | 2.74 | 6.32 | 134.5 → 126.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 2.68 | 12.92 | 99% | 0.64 | 2.04 | 4.67 | 133.9 → 125.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.80 | 2.54 | 12.08 | 99% | 0.53 | 2.04 | 4.52 | 111.1 → 106.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.48 | 10.98 | 99% | 0.57 | 2.04 | 4.66 | 136.8 → 129.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.84 | 2.64 | 16.56 | 99% | 0.64 | 2.08 | 4.20 | 136.7 → 129.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.60 | 2.62 | 9.76 | 99% | 0.56 | 1.88 | 3.65 | 72.9 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.234 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.79 | 2.31 | 2.48 | 100% | 0.47 | 1.89 | 4.85 | 78.4 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 259 frames at 120 Hz; the text stood still on 0 of them, 255 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.51 | 12.73 | 99% | 0.62 | 2.05 | 4.62 | 132.4 → 129.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.75 | 5.46 | 21.23 | 98% | 2.56 | 4.58 | 5.11 | 347.0 → 341.0 | 9 | 1.1 / 4 |

