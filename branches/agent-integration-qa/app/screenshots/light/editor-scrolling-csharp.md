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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 4.52 | 36.61 | 99% | 0.56 | 3.17 | 5.03 | 84.5 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.06 | 3.71 | 18.12 | 99% | 0.76 | 3.07 | 5.03 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 3.86 | 18.79 | 99% | 0.95 | 3.05 | 5.10 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.36 | 17.87 | 99% | 0.62 | 2.82 | 4.72 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.37 | 16.58 | 99% | 1.58 | 2.70 | 4.70 | 79.8 → 73.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 3.76 | 17.12 | 99% | 0.75 | 2.85 | 4.85 | 104.4 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.92 | 3.64 | 17.30 | 99% | 0.71 | 2.95 | 5.11 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.38 | 16.11 | 99% | 0.64 | 2.76 | 4.56 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.70 | 3.19 | 10.88 | 100% | 0.60 | 2.69 | 4.38 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.97 | 3.98 | 18.48 | 99% | 1.38 | 3.06 | 5.10 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.83 | 3.46 | 13.85 | 99% | 0.66 | 2.91 | 4.18 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.12 | 17.12 | 100% | 0.67 | 2.62 | 3.70 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.313 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.019 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 3.78 | 3.93 | 100% | 0.61 | 3.10 | 5.55 | 49.3 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 167 frames at 120 Hz; the text stood still on 0 of them, 202 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.61 | 23.77 | 99% | 0.71 | 3.03 | 5.03 | 102.7 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 4.30 | 24.01 | 98% | 0.91 | 3.47 | 5.28 | 307.7 → 299.9 | 7 | 1.1 / 4 |

