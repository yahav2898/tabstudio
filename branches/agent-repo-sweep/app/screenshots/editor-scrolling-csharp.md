# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.03 | 20.30 | 100% | 0.55 | 2.56 | 5.67 | 86.5 → 87.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 3.34 | 21.71 | 99% | 0.82 | 2.63 | 5.88 | 124.3 → 122.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 4.01 | 21.49 | 100% | 1.60 | 2.72 | 6.08 | 160.0 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.25 | 20.48 | 100% | 0.69 | 2.61 | 5.87 | 103.9 → 101.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 3.08 | 20.81 | 100% | 0.72 | 2.40 | 5.75 | 81.4 → 74.0 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 3.17 | 22.43 | 99% | 0.77 | 2.57 | 5.90 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 3.39 | 13.40 | 100% | 0.85 | 2.65 | 5.87 | 106.6 → 98.1 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 3.23 | 23.94 | 99% | 0.76 | 2.62 | 5.87 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.97 | 3.11 | 4.17 | 100% | 0.62 | 2.54 | 5.78 | 83.9 → 78.4 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 3.92 | 20.63 | 99% | 1.49 | 2.72 | 6.04 | 109.2 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.00 | 3.28 | 21.43 | 100% | 0.74 | 2.58 | 5.19 | 109.3 → 100.8 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 2.81 | 3.94 | 100% | 0.73 | 2.27 | 4.46 | 46.5 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.282 |
| ArrangingLayoutable NumberDrawing | 0.057 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.87 | 3.09 | 3.33 | 100% | 0.51 | 2.46 | 6.02 | 50.5 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 164 frames at 120 Hz; the text stood still on 0 of them, 199 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 3.30 | 21.48 | 100% | 0.69 | 2.68 | 6.06 | 104.9 → 101.6 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.53 | 3.83 | 26.31 | 99% | 0.87 | 3.01 | 6.01 | 318.4 → 310.4 | 5 | 1.1 / 4 |

