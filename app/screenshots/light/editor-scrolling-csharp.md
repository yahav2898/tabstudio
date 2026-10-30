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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 3.67 | 15.29 | 99% | 0.65 | 3.03 | 5.24 | 86.1 → 87.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.26 | 3.86 | 18.10 | 99% | 0.90 | 3.07 | 5.46 | 123.4 → 121.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.46 | 5.89 | 92.78 | 99% | 2.94 | 3.19 | 5.87 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.05 | 3.63 | 13.77 | 100% | 0.75 | 3.00 | 5.40 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 3.50 | 18.63 | 99% | 0.81 | 2.79 | 5.05 | 80.7 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 3.68 | 19.16 | 99% | 0.78 | 2.99 | 5.28 | 105.9 → 97.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.20 | 4.86 | 16.85 | 99% | 1.77 | 3.24 | 5.65 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 3.76 | 21.91 | 99% | 0.84 | 3.03 | 5.20 | 105.1 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.04 | 3.69 | 13.85 | 100% | 0.70 | 3.04 | 4.96 | 83.5 → 78.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.20 | 4.01 | 17.36 | 99% | 0.85 | 3.22 | 5.34 | 108.4 → 100.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.14 | 3.90 | 18.60 | 99% | 0.88 | 3.16 | 4.85 | 108.5 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 3.20 | 15.33 | 100% | 0.96 | 2.64 | 4.02 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.319 |
| MeasuringLayoutable LineDrawing | 0.049 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.11 | 3.72 | 16.53 | 98% | 0.75 | 2.93 | 5.62 | 50.3 → 72.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 179 frames at 120 Hz; the text stood still on 0 of them, 213 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.24 | 4.26 | 18.08 | 99% | 1.81 | 3.23 | 5.63 | 104.3 → 101.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.64 | 4.34 | 22.12 | 98% | 0.94 | 3.50 | 5.27 | 316.7 → 308.6 | 7 | 1.1 / 4 |

