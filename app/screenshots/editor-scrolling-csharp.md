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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 2.77 | 14.15 | 99% | 0.35 | 2.51 | 3.95 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.04 | 18.71 | 99% | 0.58 | 2.57 | 4.06 | 122.1 → 122.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 3.19 | 17.43 | 99% | 0.86 | 2.61 | 4.21 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 3.12 | 24.82 | 99% | 0.61 | 2.66 | 4.23 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 3.18 | 98.07 | 99% | 0.72 | 2.57 | 4.42 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.56 | 2.86 | 15.27 | 99% | 0.55 | 2.50 | 3.98 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.57 | 2.92 | 17.99 | 99% | 0.55 | 2.50 | 4.05 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.93 | 15.72 | 99% | 0.54 | 2.50 | 3.88 | 104.3 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.54 | 2.90 | 13.78 | 99% | 0.51 | 2.53 | 3.95 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.71 | 3.35 | 16.56 | 99% | 0.77 | 2.79 | 4.80 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.89 | 16.72 | 99% | 0.53 | 2.50 | 3.48 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.56 | 11.99 | 100% | 0.56 | 2.25 | 2.92 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.179 |
| AttachingStyle - | 0.116 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.49 | 2.74 | 3.05 | 100% | 0.39 | 2.40 | 4.20 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 211 frames at 120 Hz; the text stood still on 0 of them, 244 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 3.03 | 23.35 | 99% | 0.53 | 2.56 | 4.25 | 103.3 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 3.44 | 20.48 | 98% | 0.65 | 2.86 | 4.15 | 308.8 → 301.1 | 7 | 1.1 / 4 |

