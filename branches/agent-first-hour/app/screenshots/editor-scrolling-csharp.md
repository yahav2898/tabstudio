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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.20 | 13.61 | 100% | 0.44 | 2.73 | 4.60 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.46 | 17.59 | 99% | 0.68 | 2.84 | 4.67 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.63 | 17.14 | 99% | 0.87 | 2.88 | 4.83 | 158.1 → 143.1 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.62 | 35.46 | 99% | 0.68 | 2.96 | 4.90 | 102.3 → 99.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.29 | 34.82 | 100% | 0.70 | 2.72 | 5.08 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.67 | 3.25 | 19.03 | 99% | 0.59 | 2.69 | 4.52 | 105.1 → 96.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.68 | 3.21 | 17.73 | 99% | 0.60 | 2.69 | 4.57 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.69 | 3.12 | 15.77 | 99% | 0.61 | 2.69 | 4.41 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.69 | 3.29 | 13.75 | 99% | 0.56 | 2.72 | 4.63 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.87 | 3.90 | 16.96 | 99% | 1.59 | 2.85 | 5.41 | 107.6 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.69 | 3.11 | 15.89 | 99% | 0.62 | 2.68 | 3.87 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.82 | 12.78 | 100% | 1.31 | 2.45 | 3.33 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.268 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 3.16 | 3.39 | 100% | 0.45 | 2.61 | 4.62 | 49.3 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2262 ms over 186 frames at 120 Hz; the text stood still on 0 of them, 218 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.54 | 19.29 | 99% | 0.67 | 2.91 | 5.25 | 103.4 → 100.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 3.94 | 20.30 | 98% | 0.79 | 3.22 | 4.92 | 308.9 → 301.1 | 7 | 1.1 / 4 |

