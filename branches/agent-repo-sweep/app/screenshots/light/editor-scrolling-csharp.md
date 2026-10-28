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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.09 | 14.64 | 99% | 0.48 | 2.59 | 4.02 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.19 | 16.80 | 100% | 0.64 | 2.63 | 4.10 | 123.3 → 121.1 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.45 | 18.21 | 99% | 0.82 | 2.72 | 4.20 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 4.27 | 17.49 | 99% | 1.08 | 2.73 | 4.15 | 103.2 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.04 | 14.69 | 99% | 0.63 | 2.52 | 3.95 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.11 | 15.98 | 99% | 0.63 | 2.61 | 4.09 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 3.16 | 17.98 | 99% | 0.60 | 2.64 | 3.98 | 105.7 → 97.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.22 | 17.05 | 99% | 0.63 | 2.64 | 4.00 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 3.09 | 13.23 | 100% | 0.56 | 2.59 | 3.93 | 83.4 → 78.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.99 | 15.18 | 99% | 0.57 | 2.54 | 3.94 | 108.3 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 3.03 | 18.13 | 99% | 0.57 | 2.57 | 3.57 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.76 | 8.93 | 100% | 1.27 | 2.31 | 3.03 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.74 | 3.08 | 3.38 | 100% | 0.48 | 2.45 | 4.08 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 251 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.30 | 85.89 | 99% | 0.62 | 2.61 | 4.08 | 104.2 → 100.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 3.52 | 18.65 | 98% | 0.69 | 2.85 | 4.00 | 316.6 → 308.6 | 7 | 1.1 / 4 |

