# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 3.24 | 33.00 | 100% | 0.52 | 2.82 | 5.00 | 86.1 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.32 | 21.16 | 99% | 0.64 | 2.73 | 4.47 | 123.4 → 121.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.27 | 16.27 | 99% | 0.80 | 2.74 | 4.50 | 158.6 → 143.0 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.19 | 16.30 | 99% | 0.59 | 2.69 | 4.36 | 103.2 → 100.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.68 | 13.04 | 100% | 1.33 | 2.68 | 4.57 | 80.6 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 3.03 | 13.38 | 99% | 0.54 | 2.61 | 4.23 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 3.18 | 14.86 | 99% | 0.55 | 2.71 | 4.43 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.96 | 13.00 | 99% | 0.53 | 2.57 | 4.08 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.72 | 10.69 | 100% | 0.64 | 2.83 | 4.62 | 83.4 → 78.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 3.24 | 15.25 | 99% | 0.59 | 2.74 | 4.48 | 108.4 → 100.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 3.14 | 16.75 | 99% | 0.60 | 2.73 | 4.04 | 108.4 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.86 | 4.09 | 100% | 0.60 | 2.49 | 3.23 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.267 |
| AttachingStyle - | 0.044 |
| ArrangingLayoutable TextView | 0.029 |
| ArrangingLayoutable LineDrawing | 0.020 |
| FindingResource - | 0.013 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.67 | 3.34 | 3.99 | 100% | 0.54 | 2.73 | 5.27 | 50.1 → 72.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 226 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.27 | 13.03 | 99% | 0.59 | 2.77 | 4.51 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.17 | 3.68 | 18.32 | 98% | 0.67 | 3.13 | 4.52 | 316.6 → 308.6 | 7 | 1.1 / 4 |

