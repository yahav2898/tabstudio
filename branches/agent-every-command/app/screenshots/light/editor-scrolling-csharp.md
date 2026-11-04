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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.89 | 20.85 | 100% | 0.55 | 2.34 | 5.62 | 84.5 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.16 | 3.94 | 89.39 | 99% | 2.44 | 2.58 | 5.84 | 121.4 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.22 | 3.34 | 21.96 | 99% | 1.09 | 2.49 | 6.02 | 156.8 → 141.8 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 3.11 | 20.98 | 100% | 0.76 | 2.44 | 5.92 | 101.7 → 98.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.91 | 22.19 | 100% | 0.74 | 2.27 | 5.72 | 79.8 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 1.00 | 3.35 | 21.22 | 99% | 2.62 | 2.48 | 5.98 | 104.4 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.03 | 3.11 | 22.68 | 99% | 0.84 | 2.46 | 6.10 | 104.2 → 96.0 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.00 | 3.10 | 23.06 | 99% | 0.77 | 2.43 | 5.94 | 103.5 → 95.4 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.96 | 3.04 | 21.07 | 100% | 0.66 | 2.46 | 5.69 | 82.3 → 77.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.05 | 3.29 | 20.40 | 99% | 0.78 | 2.56 | 5.96 | 106.8 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.02 | 3.13 | 21.51 | 100% | 0.76 | 2.46 | 5.30 | 106.9 → 98.7 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 2.70 | 21.27 | 100% | 1.16 | 2.12 | 4.55 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.357 |
| ArrangingLayoutable TextView | 0.020 |
| AttachingStyle - | 0.020 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 3.08 | 3.15 | 100% | 0.52 | 2.40 | 5.99 | 49.3 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 174 frames at 120 Hz; the text stood still on 0 of them, 209 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.25 | 26.32 | 100% | 0.80 | 2.53 | 6.15 | 102.7 → 99.4 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.46 | 3.70 | 31.43 | 98% | 0.86 | 2.88 | 6.13 | 307.7 → 299.8 | 5 | 1.1 / 4 |

