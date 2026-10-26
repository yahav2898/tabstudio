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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 1.93 | 2.27 | 100% | 0.40 | 1.56 | 4.23 | 113.7 → 115.0 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 2.89 | 47.12 | 100% | 0.65 | 1.84 | 4.46 | 152.3 → 150.3 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 4.27 | 34.71 | 99% | 1.03 | 2.61 | 4.56 | 189.1 → 172.5 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 1.95 | 2.52 | 100% | 0.53 | 1.55 | 4.27 | 131.7 → 128.9 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 1.90 | 39.66 | 100% | 0.55 | 1.49 | 4.16 | 108.1 → 101.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 1.99 | 2.43 | 100% | 0.56 | 1.56 | 4.22 | 134.0 → 126.0 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 3.14 | 45.68 | 100% | 0.62 | 2.54 | 4.61 | 134.2 → 126.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 1.89 | 2.44 | 100% | 0.54 | 1.51 | 4.07 | 133.3 → 125.1 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 1.88 | 39.52 | 100% | 0.47 | 1.48 | 4.05 | 110.8 → 105.7 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 1.95 | 2.30 | 100% | 0.55 | 1.54 | 4.20 | 136.6 → 128.9 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.34 | 38.84 | 100% | 0.58 | 1.68 | 3.85 | 136.8 → 128.4 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.57 | 73.82 | 100% | 0.53 | 1.41 | 3.06 | 72.0 → 68.2 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.194 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 1.80 | 1.98 | 100% | 0.38 | 1.41 | 4.28 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 7 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 1.98 | 38.93 | 100% | 0.53 | 1.61 | 4.26 | 132.4 → 129.4 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 4.72 | 80.81 | 99% | 2.50 | 4.05 | 4.59 | 346.7 → 340.0 | 3 | 1.1 / 4 |

