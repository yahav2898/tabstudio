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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.14 | 18.40 | 99% | 0.49 | 2.70 | 4.85 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.46 | 17.45 | 99% | 0.69 | 2.81 | 4.95 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.57 | 18.89 | 99% | 0.95 | 2.81 | 5.31 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 3.48 | 18.52 | 99% | 0.68 | 2.91 | 5.21 | 101.7 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 4.21 | 20.61 | 99% | 1.68 | 2.71 | 5.08 | 79.8 → 73.1 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.83 | 3.41 | 24.68 | 99% | 0.69 | 2.81 | 5.06 | 104.4 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.79 | 3.44 | 18.05 | 99% | 0.69 | 2.82 | 4.99 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.21 | 14.72 | 100% | 0.61 | 2.68 | 4.74 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.79 | 3.30 | 15.24 | 99% | 0.60 | 2.82 | 4.91 | 82.3 → 77.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.90 | 3.74 | 43.75 | 99% | 1.09 | 2.95 | 5.53 | 106.8 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.95 | 3.69 | 131.17 | 99% | 0.75 | 3.01 | 4.92 | 106.9 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 3.03 | 11.51 | 100% | 0.64 | 2.55 | 3.65 | 46.1 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.261 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.96 | 3.82 | 4.25 | 100% | 0.70 | 3.06 | 5.94 | 49.3 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 185 frames at 120 Hz; the text stood still on 0 of them, 220 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 3.89 | 26.55 | 100% | 0.77 | 3.18 | 6.15 | 102.7 → 99.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 4.72 | 24.94 | 98% | 0.95 | 3.70 | 6.14 | 307.7 → 299.9 | 7 | 1.1 / 4 |

