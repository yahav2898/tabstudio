# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.34 | 14.63 | 100% | 0.47 | 2.84 | 4.70 | 85.2 → 85.8 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.84 | 81.27 | 99% | 1.19 | 3.02 | 4.98 | 122.4 → 123.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 3.68 | 17.48 | 99% | 0.91 | 2.93 | 4.80 | 158.4 → 143.3 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.39 | 16.81 | 99% | 0.64 | 2.85 | 4.67 | 102.5 → 99.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.32 | 16.69 | 100% | 1.00 | 2.67 | 4.53 | 80.2 → 73.4 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.62 | 3.06 | 14.84 | 99% | 0.54 | 2.61 | 4.27 | 105.2 → 96.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.68 | 3.27 | 15.09 | 99% | 0.58 | 2.79 | 4.57 | 105.1 → 96.9 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.69 | 3.89 | 74.47 | 99% | 1.10 | 2.78 | 4.64 | 104.5 → 96.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.62 | 3.09 | 13.18 | 99% | 0.50 | 2.65 | 4.19 | 82.4 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.70 | 3.21 | 22.24 | 99% | 0.61 | 2.68 | 4.66 | 107.6 → 99.7 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.85 | 3.50 | 74.10 | 99% | 0.76 | 2.85 | 4.54 | 107.8 → 99.5 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.95 | 10.12 | 100% | 0.79 | 2.51 | 3.61 | 45.7 → 42.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 61.3

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.290 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 3.28 | 3.31 | 100% | 0.44 | 2.65 | 4.84 | 49.4 → 73.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 198 frames at 120 Hz; the text stood still on 0 of them, 231 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.60 | 14.46 | 99% | 0.72 | 2.97 | 4.98 | 103.6 → 100.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 4.10 | 20.22 | 98% | 0.77 | 3.33 | 4.79 | 308.4 → 300.5 | 8 | 1.1 / 4 |

