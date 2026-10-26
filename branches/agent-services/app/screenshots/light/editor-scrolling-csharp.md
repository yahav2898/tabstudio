# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.91 | 10.80 | 99% | 0.36 | 1.64 | 4.09 | 115.6 → 117.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.39 | 13.12 | 99% | 0.73 | 2.56 | 4.61 | 153.7 → 152.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.23 | 13.33 | 99% | 0.70 | 1.67 | 4.14 | 189.3 → 173.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.07 | 11.69 | 99% | 0.52 | 1.65 | 4.13 | 133.3 → 130.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.84 | 8.32 | 100% | 0.50 | 1.47 | 3.83 | 109.6 → 102.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.09 | 13.80 | 99% | 0.78 | 2.42 | 4.40 | 135.8 → 128.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.23 | 10.87 | 99% | 0.52 | 1.97 | 4.02 | 135.6 → 127.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.64 | 10.67 | 99% | 0.52 | 2.28 | 4.00 | 134.9 → 127.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.58 | 9.24 | 99% | 0.47 | 2.22 | 3.91 | 112.8 → 107.9 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 3.19 | 12.65 | 99% | 0.63 | 2.54 | 4.50 | 138.7 → 130.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 2.58 | 10.79 | 99% | 0.51 | 2.23 | 3.72 | 138.3 → 130.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.19 | 7.02 | 100% | 0.44 | 1.95 | 3.08 | 73.6 → 70.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.177 |
| ArrangingLayoutable ScrollingTextLayer | 0.039 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 2.35 | 2.67 | 100% | 0.39 | 2.14 | 4.22 | 80.6 → 102.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 250 frames at 120 Hz; the text stood still on 0 of them, 252 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.43 | 10.56 | 99% | 0.50 | 2.15 | 4.05 | 133.9 → 131.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 3.22 | 15.39 | 98% | 0.62 | 2.69 | 4.18 | 346.0 → 339.5 | 8 | 1.1 / 4 |

