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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 2.09 | 12.85 | 99% | 0.24 | 1.90 | 2.43 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 2.22 | 14.64 | 99% | 0.40 | 1.95 | 2.56 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 2.32 | 15.49 | 99% | 0.72 | 1.97 | 2.63 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.41 | 2.22 | 15.05 | 99% | 0.36 | 1.93 | 2.63 | 102.2 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.35 | 11.98 | 100% | 0.46 | 1.99 | 2.70 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.49 | 2.38 | 16.60 | 99% | 0.45 | 2.03 | 2.86 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.62 | 2.77 | 18.30 | 99% | 1.30 | 2.19 | 3.06 | 104.9 → 96.6 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.18 | 13.56 | 99% | 1.20 | 2.43 | 3.30 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.38 | 2.18 | 10.20 | 100% | 0.33 | 1.95 | 2.50 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.40 | 2.27 | 14.00 | 99% | 0.35 | 1.96 | 2.61 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.40 | 2.32 | 17.33 | 99% | 0.43 | 1.96 | 2.31 | 107.5 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 2.03 | 8.85 | 100% | 1.01 | 1.82 | 1.84 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable CachedLineNumberMargin | 0.049 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.38 | 2.17 | 2.26 | 100% | 0.31 | 1.87 | 2.60 | 49.3 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 25 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 4.08 | 13.16 | 99% | 0.50 | 2.39 | 2.91 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.62 | 2.52 | 18.52 | 98% | 0.42 | 2.16 | 2.68 | 308.8 → 301.0 | 7 | 1.1 / 4 |

