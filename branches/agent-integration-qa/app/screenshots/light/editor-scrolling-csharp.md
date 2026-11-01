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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.97 | 12.39 | 100% | 0.40 | 2.62 | 4.26 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.35 | 16.82 | 99% | 0.65 | 2.76 | 4.47 | 121.3 → 121.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 5.36 | 31.61 | 99% | 2.61 | 3.00 | 5.20 | 156.8 → 141.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 3.21 | 15.36 | 99% | 0.60 | 2.70 | 4.47 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.93 | 13.82 | 100% | 0.59 | 2.52 | 4.26 | 79.7 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.62 | 3.07 | 17.74 | 99% | 0.57 | 2.60 | 4.31 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.70 | 3.20 | 16.06 | 99% | 0.61 | 2.69 | 4.59 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 4.04 | 13.10 | 99% | 0.80 | 2.82 | 5.04 | 103.6 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.57 | 2.96 | 12.99 | 99% | 0.47 | 2.56 | 4.02 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.67 | 3.09 | 15.59 | 99% | 0.60 | 2.64 | 4.45 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.64 | 3.10 | 17.64 | 99% | 0.63 | 2.62 | 3.82 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.75 | 9.19 | 100% | 1.04 | 2.37 | 3.29 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.333 |
| AttachingStyle - | 0.082 |
| ArrangingLayoutable TextView | 0.029 |
| FindingResource - | 0.013 |
| RaisingRoutedEvent ScrollBar | 0.013 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.65 | 3.27 | 34.11 | 98% | 0.46 | 2.61 | 4.67 | 49.3 → 72.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 240 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.29 | 15.15 | 99% | 0.61 | 2.75 | 4.54 | 102.7 → 99.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 5.72 | 22.84 | 98% | 1.40 | 3.33 | 4.88 | 307.6 → 299.9 | 8 | 1.1 / 4 |

