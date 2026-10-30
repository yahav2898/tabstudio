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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.29 | 14.34 | 100% | 0.45 | 2.89 | 4.48 | 85.1 → 85.8 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 4.22 | 33.99 | 99% | 1.17 | 3.13 | 5.14 | 122.4 → 123.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.82 | 18.28 | 99% | 0.97 | 3.10 | 4.82 | 158.4 → 143.3 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.49 | 17.93 | 99% | 0.62 | 3.01 | 4.64 | 102.5 → 99.7 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.50 | 15.12 | 99% | 0.73 | 2.91 | 4.57 | 80.2 → 73.4 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 3.62 | 16.26 | 99% | 0.68 | 2.97 | 4.72 | 105.3 → 96.9 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.72 | 3.49 | 17.86 | 99% | 0.65 | 2.93 | 4.61 | 105.1 → 96.9 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 3.44 | 17.49 | 99% | 0.63 | 2.91 | 4.50 | 104.4 → 96.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.77 | 3.89 | 85.73 | 99% | 1.32 | 2.99 | 4.61 | 82.4 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.73 | 3.38 | 19.92 | 99% | 0.61 | 2.90 | 4.55 | 107.7 → 99.7 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.66 | 3.37 | 14.57 | 99% | 0.60 | 2.87 | 4.01 | 107.7 → 99.5 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.83 | 3.81 | 100% | 0.59 | 2.49 | 3.21 | 45.7 → 42.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 61.3

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.360 |
| AttachingStyle - | 0.094 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.63 | 3.35 | 3.38 | 100% | 0.46 | 2.77 | 4.81 | 49.4 → 73.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 216 frames at 120 Hz; the text stood still on 0 of them, 249 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.54 | 17.84 | 99% | 0.62 | 3.00 | 4.66 | 103.5 → 100.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.45 | 6.08 | 22.22 | 98% | 0.90 | 5.38 | 5.04 | 308.8 → 301.1 | 8 | 1.1 / 4 |

