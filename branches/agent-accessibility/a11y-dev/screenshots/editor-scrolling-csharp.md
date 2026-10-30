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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.95 | 11.89 | 100% | 0.39 | 2.63 | 4.28 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.58 | 15.59 | 100% | 1.20 | 2.92 | 4.77 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 3.45 | 16.86 | 99% | 0.86 | 2.79 | 4.63 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.26 | 16.65 | 99% | 0.58 | 2.78 | 4.64 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.39 | 12.49 | 100% | 0.67 | 2.76 | 5.43 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 3.44 | 14.76 | 99% | 0.61 | 2.84 | 4.93 | 104.5 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.57 | 2.94 | 15.22 | 99% | 0.54 | 2.60 | 4.14 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.96 | 13.37 | 99% | 0.54 | 2.55 | 4.03 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.64 | 3.96 | 11.54 | 99% | 0.53 | 2.81 | 4.42 | 82.0 → 76.7 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.55 | 2.90 | 13.73 | 99% | 0.50 | 2.57 | 4.04 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.93 | 13.99 | 99% | 0.51 | 2.52 | 3.55 | 106.9 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.77 | 11.38 | 100% | 0.59 | 2.36 | 3.20 | 45.7 → 42.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 58.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable LineDrawing | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 3.03 | 3.12 | 100% | 0.37 | 2.54 | 4.59 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 260 frames at 120 Hz; the text stood still on 0 of them, 256 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.86 | 18.72 | 99% | 0.50 | 2.54 | 4.06 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.09 | 4.71 | 21.07 | 98% | 0.77 | 3.13 | 4.46 | 306.8 → 299.0 | 8 | 1.1 / 4 |

