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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 3.51 | 11.50 | 100% | 0.53 | 2.98 | 4.81 | 86.5 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.17 | 4.55 | 16.99 | 99% | 1.39 | 3.16 | 5.31 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 3.71 | 18.85 | 99% | 0.90 | 2.97 | 4.85 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.88 | 3.42 | 16.56 | 99% | 0.62 | 2.86 | 4.73 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.26 | 17.04 | 99% | 0.69 | 2.69 | 4.70 | 81.3 → 74.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.95 | 3.48 | 85.33 | 99% | 0.76 | 2.87 | 5.18 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 3.41 | 17.34 | 99% | 0.68 | 2.82 | 4.71 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.39 | 14.50 | 99% | 0.68 | 2.83 | 4.77 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 3.36 | 13.12 | 99% | 0.64 | 2.83 | 4.75 | 83.8 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 3.42 | 14.56 | 99% | 0.66 | 2.83 | 5.05 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 3.37 | 16.31 | 99% | 0.70 | 2.79 | 4.16 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.97 | 9.05 | 100% | 1.17 | 2.51 | 3.37 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.237 |
| AttachingStyle - | 0.023 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 3.57 | 3.68 | 100% | 0.55 | 2.84 | 5.14 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 180 frames at 120 Hz; the text stood still on 0 of them, 215 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.48 | 15.40 | 99% | 0.65 | 2.88 | 4.69 | 104.9 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 3.90 | 16.74 | 98% | 0.77 | 3.19 | 4.60 | 318.4 → 310.4 | 7 | 1.1 / 4 |

