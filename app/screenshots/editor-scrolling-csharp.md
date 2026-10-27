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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 3.75 | 10.50 | 99% | 0.53 | 3.31 | 5.23 | 124.7 → 125.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 4.11 | 17.97 | 99% | 0.84 | 3.41 | 5.37 | 162.8 → 160.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 4.06 | 16.97 | 99% | 0.87 | 3.28 | 5.17 | 198.1 → 182.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 3.67 | 14.06 | 99% | 0.63 | 3.16 | 5.10 | 142.0 → 139.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 3.79 | 12.54 | 99% | 0.70 | 3.17 | 5.21 | 118.9 → 112.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.03 | 3.97 | 13.96 | 99% | 0.68 | 3.29 | 5.36 | 144.8 → 136.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 3.64 | 13.11 | 99% | 0.63 | 3.14 | 5.08 | 144.1 → 135.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 3.59 | 12.06 | 99% | 0.63 | 3.07 | 4.87 | 143.0 → 135.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 3.88 | 12.31 | 99% | 0.65 | 3.26 | 5.26 | 121.2 → 116.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 3.73 | 14.73 | 99% | 0.64 | 3.20 | 5.46 | 147.2 → 139.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.96 | 3.70 | 14.22 | 99% | 0.69 | 3.15 | 4.64 | 146.1 → 138.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 3.25 | 7.84 | 100% | 0.57 | 2.84 | 3.85 | 81.7 → 78.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.325 |
| ArrangingLayoutable TextView | 0.022 |
| MeasuringLayoutable NumberDrawing | 0.022 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.99 | 3.56 | 4.09 | 100% | 0.49 | 3.02 | 5.39 | 88.6 → 111.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 199 frames at 120 Hz; the text stood still on 0 of them, 232 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.78 | 17.52 | 99% | 0.64 | 3.24 | 5.32 | 142.9 → 138.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 4.26 | 18.37 | 98% | 0.72 | 3.52 | 5.01 | 355.0 → 348.4 | 9 | 1.1 / 4 |

