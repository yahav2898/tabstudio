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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.80 | 10.88 | 100% | 0.35 | 2.50 | 4.00 | 84.8 → 85.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.17 | 14.13 | 99% | 0.60 | 2.65 | 4.21 | 122.1 → 122.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 3.43 | 15.42 | 99% | 1.15 | 2.69 | 4.31 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.51 | 74.20 | 99% | 0.75 | 2.83 | 4.51 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.81 | 13.22 | 100% | 0.57 | 2.44 | 3.99 | 80.4 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.56 | 2.99 | 15.68 | 99% | 0.55 | 2.55 | 4.06 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.62 | 3.10 | 6.39 | 100% | 0.59 | 2.64 | 4.30 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.10 | 13.85 | 99% | 0.59 | 2.62 | 4.39 | 104.3 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.52 | 2.87 | 12.82 | 100% | 0.47 | 2.52 | 3.94 | 82.7 → 77.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.57 | 2.90 | 15.54 | 99% | 0.53 | 2.52 | 4.06 | 107.4 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.69 | 3.43 | 15.92 | 99% | 1.48 | 2.68 | 3.90 | 107.5 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.63 | 11.70 | 100% | 1.03 | 2.29 | 2.93 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.46 | 2.92 | 3.19 | 100% | 0.34 | 2.49 | 4.04 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 241 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 3.03 | 11.90 | 100% | 0.55 | 2.60 | 4.30 | 103.3 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 3.54 | 17.65 | 98% | 0.67 | 2.93 | 4.20 | 308.8 → 301.0 | 7 | 1.1 / 4 |

