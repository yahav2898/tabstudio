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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.32 | 2.08 | 14.09 | 99% | 0.26 | 1.89 | 2.74 | 84.5 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 2.18 | 12.88 | 99% | 0.38 | 1.89 | 2.49 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.47 | 2.27 | 15.20 | 99% | 0.55 | 1.90 | 2.60 | 156.7 → 141.8 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 2.69 | 15.04 | 99% | 0.50 | 2.23 | 2.63 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 2.62 | 9.49 | 99% | 0.73 | 2.22 | 2.88 | 79.8 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.33 | 2.01 | 13.11 | 99% | 0.34 | 1.77 | 2.39 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.35 | 2.03 | 13.29 | 99% | 0.37 | 1.79 | 2.49 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.34 | 2.02 | 13.28 | 99% | 0.34 | 1.79 | 2.35 | 103.5 → 95.4 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.32 | 2.01 | 9.72 | 100% | 0.28 | 1.79 | 2.34 | 82.3 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.36 | 2.11 | 10.81 | 99% | 0.33 | 1.83 | 2.53 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.34 | 2.10 | 11.96 | 99% | 0.32 | 1.84 | 2.29 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 2.13 | 11.04 | 100% | 1.03 | 1.73 | 1.68 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.663 |
| AttachingStyle - | 0.076 |
| ArrangingLayoutable CachedLineNumberMargin | 0.026 |
| ArrangingLayoutable NumberDrawing | 0.024 |
| ArrangingLayoutable Border | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.31 | 1.97 | 2.04 | 100% | 0.24 | 1.76 | 2.51 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 7 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 2.00 | 13.74 | 100% | 0.33 | 1.81 | 2.43 | 102.6 → 99.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 2.31 | 15.61 | 98% | 0.38 | 2.00 | 2.54 | 307.6 → 299.8 | 7 | 1.1 / 4 |

