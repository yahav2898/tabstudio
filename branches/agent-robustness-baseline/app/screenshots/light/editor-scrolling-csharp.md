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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.21 | 14.60 | 100% | 0.44 | 2.69 | 4.31 | 86.0 → 87.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 3.01 | 14.23 | 100% | 0.59 | 2.56 | 4.13 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 3.19 | 14.77 | 99% | 0.75 | 2.62 | 4.34 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.95 | 13.70 | 99% | 0.54 | 2.58 | 4.17 | 103.2 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.99 | 15.00 | 99% | 0.94 | 2.48 | 4.20 | 80.6 → 73.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 2.83 | 11.40 | 100% | 0.52 | 2.46 | 4.01 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.89 | 14.41 | 99% | 0.50 | 2.51 | 4.09 | 105.7 → 97.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 2.77 | 14.72 | 99% | 0.50 | 2.46 | 3.97 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 2.99 | 11.81 | 99% | 0.52 | 2.61 | 4.08 | 83.4 → 78.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 2.95 | 12.28 | 100% | 0.50 | 2.54 | 4.07 | 108.3 → 100.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 2.87 | 14.58 | 99% | 0.50 | 2.54 | 3.61 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 2.52 | 9.49 | 100% | 0.48 | 2.24 | 2.92 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| ArrangingLayoutable TextView | 0.050 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 3.03 | 5.33 | 100% | 0.43 | 2.50 | 4.44 | 50.1 → 72.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 220 frames at 120 Hz; the text stood still on 0 of them, 253 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.86 | 11.81 | 99% | 0.48 | 2.50 | 4.07 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 3.42 | 16.72 | 98% | 0.60 | 2.89 | 4.19 | 316.5 → 308.6 | 7 | 1.1 / 4 |

