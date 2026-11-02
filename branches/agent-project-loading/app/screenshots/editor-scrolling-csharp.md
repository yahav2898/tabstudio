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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.93 | 12.01 | 100% | 0.38 | 2.55 | 4.16 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.10 | 18.81 | 99% | 0.62 | 2.64 | 4.30 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 3.32 | 18.00 | 99% | 0.86 | 2.67 | 4.49 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 3.07 | 17.78 | 99% | 0.56 | 2.61 | 4.37 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.07 | 25.78 | 99% | 0.60 | 2.58 | 4.40 | 80.5 → 73.6 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.68 | 3.27 | 33.40 | 99% | 0.65 | 2.70 | 4.54 | 105.2 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.66 | 3.08 | 13.23 | 99% | 0.58 | 2.64 | 4.51 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.61 | 3.01 | 17.66 | 99% | 0.59 | 2.55 | 4.15 | 104.2 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.56 | 2.85 | 13.43 | 100% | 0.47 | 2.50 | 4.08 | 82.6 → 77.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.63 | 3.00 | 14.37 | 99% | 0.56 | 2.55 | 4.30 | 107.5 → 99.4 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.75 | 4.24 | 18.53 | 99% | 1.03 | 2.76 | 3.99 | 107.6 → 99.3 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.61 | 14.29 | 100% | 0.51 | 2.26 | 3.02 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.188 |
| ArrangingLayoutable ScrollingTextLayer | 0.056 |
| FindingResource - | 0.054 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 2.93 | 3.22 | 100% | 0.43 | 2.44 | 4.28 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 240 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 3.34 | 25.09 | 99% | 0.66 | 2.75 | 4.80 | 103.3 → 100.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.19 | 4.83 | 20.35 | 98% | 0.79 | 3.09 | 4.71 | 308.9 → 301.0 | 7 | 1.1 / 4 |

