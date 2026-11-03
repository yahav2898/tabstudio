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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.30 | 1.96 | 11.04 | 100% | 0.22 | 1.79 | 2.30 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 2.17 | 29.65 | 99% | 0.45 | 1.86 | 2.84 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.44 | 2.20 | 14.59 | 99% | 0.49 | 1.89 | 2.51 | 158.1 → 143.1 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.34 | 2.07 | 14.12 | 99% | 0.32 | 1.84 | 2.41 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.28 | 2.02 | 10.94 | 99% | 0.33 | 1.77 | 2.25 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.33 | 2.00 | 16.69 | 99% | 0.31 | 1.79 | 2.32 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.44 | 2.74 | 16.23 | 99% | 0.50 | 2.19 | 2.61 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.45 | 2.82 | 13.01 | 99% | 0.60 | 2.27 | 2.91 | 104.3 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.32 | 2.01 | 11.07 | 100% | 0.28 | 1.80 | 2.29 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.35 | 2.04 | 13.12 | 99% | 0.32 | 1.81 | 2.44 | 107.5 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.33 | 2.05 | 12.66 | 100% | 0.31 | 1.82 | 2.06 | 107.6 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 1.80 | 8.99 | 100% | 0.47 | 1.64 | 1.63 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.166 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.28 | 1.91 | 2.00 | 100% | 0.23 | 1.75 | 2.36 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 16 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 3.03 | 16.53 | 100% | 1.31 | 2.29 | 2.97 | 103.3 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.58 | 2.40 | 17.79 | 98% | 0.38 | 2.03 | 2.61 | 308.9 → 301.0 | 7 | 1.1 / 4 |

