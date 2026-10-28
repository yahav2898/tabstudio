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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.71 | 9.13 | 99% | 0.36 | 2.44 | 3.54 | 123.5 → 124.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 3.10 | 12.51 | 99% | 0.52 | 2.67 | 3.87 | 162.0 → 159.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.51 | 16.29 | 99% | 0.67 | 2.86 | 4.17 | 198.5 → 182.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 4.72 | 17.38 | 99% | 0.63 | 3.05 | 4.19 | 142.0 → 139.5 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.12 | 11.05 | 99% | 0.52 | 2.67 | 3.92 | 118.7 → 111.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 3.05 | 15.01 | 99% | 0.55 | 2.63 | 3.82 | 143.8 → 135.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 3.58 | 14.22 | 99% | 0.57 | 3.03 | 4.63 | 144.3 → 136.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.13 | 12.86 | 99% | 0.56 | 2.67 | 3.77 | 143.0 → 135.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.99 | 11.32 | 99% | 0.48 | 2.63 | 3.61 | 120.5 → 115.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 5.06 | 97.05 | 99% | 0.64 | 3.09 | 4.32 | 147.0 → 139.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 3.05 | 10.55 | 99% | 0.50 | 2.60 | 3.41 | 146.4 → 138.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.69 | 8.62 | 100% | 0.48 | 2.35 | 2.78 | 81.7 → 77.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.57 | 2.96 | 3.10 | 100% | 0.38 | 2.52 | 3.95 | 88.1 → 109.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 3.54 | 17.23 | 99% | 0.67 | 3.00 | 4.55 | 142.7 → 139.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.14 | 6.22 | 22.93 | 98% | 2.52 | 5.54 | 4.94 | 358.0 → 351.2 | 8 | 1.1 / 4 |

