# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.27 | 1.15 | 7.33 | 100% | 0.19 | 0.98 | 2.15 | 86.4 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.35 | 1.27 | 11.04 | 99% | 0.33 | 1.04 | 2.27 | 124.1 → 121.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.39 | 1.32 | 11.47 | 99% | 0.44 | 1.04 | 2.31 | 159.9 → 144.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.30 | 1.19 | 10.97 | 99% | 0.29 | 1.00 | 2.26 | 103.9 → 100.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.32 | 2.36 | 8.02 | 100% | 0.42 | 1.05 | 2.21 | 81.3 → 73.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.36 | 2.63 | 77.14 | 99% | 0.42 | 1.33 | 2.70 | 106.6 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.29 | 1.22 | 12.09 | 99% | 0.30 | 1.00 | 2.23 | 106.4 → 98.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.29 | 1.18 | 9.71 | 99% | 0.29 | 0.98 | 2.19 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.28 | 1.14 | 8.83 | 100% | 0.23 | 0.98 | 2.11 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.30 | 1.19 | 10.97 | 99% | 0.29 | 0.99 | 2.24 | 109.0 → 100.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.29 | 1.17 | 9.07 | 100% | 0.28 | 0.97 | 1.90 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.16 | 0.99 | 7.31 | 100% | 0.39 | 0.85 | 1.53 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.135 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.004 |
| FindingResource - | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable Grid | 0.000 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.35 | 1.51 | 2.73 | 100% | 0.32 | 1.06 | 2.44 | 50.2 → 72.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 23 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.30 | 1.21 | 11.13 | 99% | 0.29 | 1.01 | 2.24 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.53 | 1.47 | 12.09 | 98% | 0.33 | 1.19 | 2.33 | 318.3 → 310.3 | 7 | 1.1 / 4 |

