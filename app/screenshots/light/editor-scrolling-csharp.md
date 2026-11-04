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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 3.55 | 14.26 | 100% | 0.62 | 2.99 | 5.37 | 84.4 → 85.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 3.84 | 17.44 | 99% | 0.86 | 3.04 | 5.36 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.51 | 5.23 | 88.62 | 99% | 2.74 | 3.12 | 5.46 | 156.8 → 141.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 3.75 | 12.92 | 99% | 0.81 | 3.03 | 5.18 | 101.7 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 3.52 | 17.38 | 99% | 0.79 | 2.80 | 4.96 | 79.8 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 1.08 | 3.90 | 14.52 | 99% | 0.84 | 3.03 | 5.24 | 104.4 → 96.1 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.13 | 3.99 | 14.86 | 99% | 0.95 | 3.08 | 5.28 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.10 | 3.71 | 19.15 | 99% | 0.81 | 3.01 | 5.12 | 103.5 → 95.4 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.99 | 3.55 | 15.75 | 99% | 0.70 | 2.89 | 4.99 | 82.3 → 77.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.18 | 3.92 | 84.49 | 99% | 0.93 | 3.02 | 5.28 | 106.7 → 98.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.97 | 3.61 | 16.98 | 99% | 0.75 | 2.90 | 4.60 | 106.9 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.11 | 12.14 | 100% | 1.20 | 2.54 | 3.74 | 46.0 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.329 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.020 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.96 | 3.51 | 3.64 | 100% | 0.59 | 2.86 | 5.37 | 49.3 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 188 frames at 120 Hz; the text stood still on 0 of them, 223 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 3.70 | 18.04 | 99% | 0.75 | 2.99 | 5.30 | 102.7 → 99.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.50 | 4.21 | 22.24 | 98% | 0.88 | 3.34 | 5.15 | 307.7 → 299.9 | 8 | 1.1 / 4 |

