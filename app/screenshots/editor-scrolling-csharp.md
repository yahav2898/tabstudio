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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.81 | 3.55 | 34.46 | 99% | 0.96 | 2.99 | 4.89 | 86.5 → 87.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 3.51 | 34.81 | 99% | 0.73 | 2.87 | 4.76 | 124.2 → 122.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 3.62 | 16.55 | 99% | 0.98 | 2.85 | 4.80 | 160.0 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.36 | 16.69 | 99% | 0.65 | 2.81 | 4.98 | 103.9 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 3.31 | 14.89 | 100% | 0.73 | 2.68 | 4.82 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 3.34 | 17.22 | 99% | 0.64 | 2.79 | 4.79 | 106.6 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 3.36 | 17.04 | 99% | 0.67 | 2.78 | 4.73 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 3.38 | 17.50 | 99% | 0.66 | 2.82 | 4.75 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 3.72 | 93.87 | 99% | 0.74 | 2.92 | 5.05 | 83.8 → 78.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 3.38 | 17.11 | 99% | 0.63 | 2.82 | 5.00 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 3.37 | 13.49 | 99% | 0.68 | 2.82 | 4.23 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.93 | 13.04 | 100% | 0.66 | 2.49 | 3.49 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable NumberDrawing | 0.053 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 3.74 | 5.28 | 100% | 0.60 | 3.24 | 5.55 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 171 frames at 120 Hz; the text stood still on 0 of them, 206 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.99 | 3.64 | 14.85 | 99% | 0.68 | 3.00 | 5.23 | 104.9 → 101.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.40 | 3.99 | 18.43 | 98% | 0.86 | 3.26 | 5.00 | 318.4 → 310.4 | 8 | 1.1 / 4 |

