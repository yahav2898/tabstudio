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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.90 | 7.97 | 100% | 0.47 | 2.42 | 4.18 | 113.8 → 114.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 1.79 | 10.29 | 99% | 0.49 | 1.42 | 3.55 | 151.6 → 149.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 1.85 | 12.61 | 99% | 0.57 | 1.38 | 3.52 | 187.5 → 171.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 1.60 | 9.17 | 99% | 0.41 | 1.28 | 3.32 | 130.6 → 128.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.68 | 8.04 | 100% | 0.45 | 1.34 | 3.45 | 107.3 → 100.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 2.88 | 11.66 | 99% | 0.53 | 2.31 | 3.87 | 133.8 → 125.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.84 | 11.54 | 100% | 0.50 | 1.43 | 3.61 | 133.3 → 125.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.56 | 9.14 | 99% | 0.43 | 1.25 | 3.30 | 132.3 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.49 | 7.98 | 100% | 0.37 | 1.21 | 3.18 | 109.9 → 104.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.54 | 1.95 | 10.41 | 99% | 0.45 | 1.41 | 3.56 | 135.5 → 128.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 3.37 | 12.58 | 99% | 0.67 | 2.44 | 3.60 | 136.5 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.26 | 4.95 | 100% | 0.36 | 1.02 | 2.51 | 71.0 → 67.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.145 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.41 | 1.52 | 1.54 | 100% | 0.34 | 1.24 | 3.46 | 76.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.52 | 1.61 | 9.32 | 99% | 0.42 | 1.28 | 3.37 | 131.3 → 128.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.95 | 3.69 | 14.55 | 98% | 0.56 | 2.86 | 3.99 | 345.8 → 338.3 | 7 | 1.1 / 4 |

