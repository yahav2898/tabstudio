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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.07 | 16.59 | 99% | 0.46 | 2.68 | 4.56 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.26 | 17.38 | 99% | 0.62 | 2.76 | 4.62 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.58 | 19.51 | 99% | 0.97 | 2.89 | 5.11 | 158.2 → 143.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 4.04 | 36.61 | 99% | 0.81 | 3.02 | 5.33 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.31 | 26.46 | 99% | 0.64 | 2.76 | 4.82 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.77 | 3.30 | 19.79 | 99% | 0.67 | 2.80 | 4.85 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 3.41 | 19.12 | 99% | 0.66 | 2.89 | 5.01 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.33 | 16.77 | 99% | 0.68 | 2.73 | 4.66 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.87 | 3.48 | 11.36 | 100% | 0.64 | 2.98 | 5.05 | 82.8 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.85 | 3.33 | 17.54 | 99% | 0.66 | 2.79 | 4.99 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 3.33 | 19.44 | 99% | 0.68 | 2.81 | 4.24 | 107.5 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.78 | 9.45 | 100% | 1.30 | 2.41 | 3.37 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.268 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.00 | 3.60 | 3.71 | 100% | 0.57 | 2.90 | 5.72 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 213 frames at 120 Hz; the text stood still on 0 of them, 248 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.27 | 14.49 | 99% | 0.60 | 2.78 | 4.73 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.13 | 3.64 | 20.40 | 98% | 0.71 | 3.08 | 4.64 | 308.9 → 301.1 | 7 | 1.1 / 4 |

