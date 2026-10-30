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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.00 | 3.65 | 18.67 | 99% | 0.60 | 3.06 | 5.23 | 87.2 → 88.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.23 | 3.89 | 18.47 | 99% | 0.97 | 3.06 | 5.27 | 125.4 → 123.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.50 | 5.63 | 19.17 | 99% | 2.27 | 3.14 | 5.41 | 161.7 → 145.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.13 | 3.75 | 17.05 | 99% | 0.77 | 3.06 | 5.15 | 104.9 → 101.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.60 | 11.71 | 100% | 0.76 | 2.88 | 4.90 | 81.9 → 74.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 1.11 | 3.81 | 18.10 | 99% | 0.87 | 3.06 | 5.17 | 107.6 → 99.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.13 | 3.97 | 17.15 | 99% | 0.78 | 3.09 | 5.14 | 107.5 → 99.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.78 | 18.38 | 99% | 0.80 | 3.05 | 5.05 | 106.8 → 98.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 3.63 | 4.61 | 100% | 0.68 | 2.97 | 4.83 | 84.4 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.15 | 3.91 | 93.30 | 99% | 0.86 | 3.12 | 5.06 | 110.1 → 102.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.06 | 3.67 | 17.25 | 99% | 0.75 | 3.00 | 4.38 | 110.2 → 101.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.63 | 3.12 | 15.29 | 100% | 0.75 | 2.60 | 3.65 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.273 |
| AttachingStyle - | 0.065 |
| ArrangingLayoutable NumberDrawing | 0.060 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 3.67 | 3.80 | 100% | 0.56 | 2.90 | 5.06 | 50.5 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 187 frames at 120 Hz; the text stood still on 0 of them, 222 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 3.74 | 20.00 | 99% | 0.95 | 3.03 | 5.06 | 105.8 → 102.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 4.24 | 19.30 | 98% | 0.90 | 3.37 | 5.05 | 320.6 → 312.5 | 7 | 1.1 / 4 |

