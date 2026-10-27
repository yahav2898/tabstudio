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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.05 | 10.40 | 99% | 0.37 | 1.76 | 4.11 | 121.6 → 123.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.30 | 11.40 | 99% | 0.56 | 1.81 | 4.19 | 159.4 → 157.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.76 | 15.16 | 99% | 0.88 | 2.52 | 4.46 | 194.4 → 179.6 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.91 | 10.56 | 99% | 0.51 | 2.37 | 4.32 | 139.4 → 136.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.94 | 9.20 | 100% | 0.48 | 1.61 | 3.91 | 115.9 → 109.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.66 | 2.08 | 13.81 | 99% | 0.51 | 1.72 | 4.15 | 141.4 → 133.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 2.14 | 11.28 | 99% | 0.51 | 1.75 | 4.12 | 141.3 → 133.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.98 | 81.43 | 99% | 0.60 | 2.49 | 4.32 | 141.0 → 133.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.07 | 8.69 | 100% | 0.44 | 1.71 | 4.04 | 119.0 → 114.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.19 | 11.98 | 99% | 0.53 | 1.76 | 4.11 | 143.9 → 136.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.10 | 11.10 | 99% | 0.52 | 1.76 | 3.64 | 143.7 → 136.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.73 | 6.46 | 100% | 0.43 | 1.45 | 3.06 | 79.9 → 76.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.247 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| FindingResource - | 0.020 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
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
| touch drag and flick (real touch input) | 46 | 0.72 | 2.65 | 5.35 | 100% | 0.39 | 2.40 | 4.67 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.18 | 11.80 | 99% | 0.53 | 1.78 | 4.13 | 139.8 → 137.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 3.26 | 16.15 | 98% | 0.62 | 2.78 | 4.23 | 350.9 → 344.2 | 8 | 1.1 / 4 |

