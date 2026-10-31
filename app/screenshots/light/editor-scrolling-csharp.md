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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.93 | 13.36 | 100% | 0.42 | 2.61 | 4.26 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 3.32 | 17.32 | 99% | 0.77 | 2.77 | 4.49 | 121.3 → 121.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 4.47 | 30.90 | 99% | 2.47 | 2.89 | 4.80 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.99 | 15.83 | 99% | 0.53 | 2.60 | 4.15 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.77 | 11.94 | 100% | 0.55 | 2.41 | 3.94 | 79.7 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.54 | 2.90 | 16.04 | 99% | 0.51 | 2.50 | 4.04 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.60 | 3.07 | 15.88 | 99% | 0.62 | 2.58 | 4.16 | 104.1 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.61 | 3.18 | 15.58 | 99% | 0.55 | 2.59 | 4.05 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.51 | 2.78 | 11.92 | 99% | 0.44 | 2.43 | 3.83 | 82.2 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.57 | 2.86 | 12.31 | 99% | 0.54 | 2.49 | 4.04 | 106.6 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.58 | 2.88 | 14.38 | 99% | 0.52 | 2.53 | 3.52 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.79 | 11.79 | 100% | 1.37 | 2.40 | 3.31 | 46.0 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| AttachingStyle - | 0.028 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable Track | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 2.92 | 3.34 | 100% | 0.41 | 2.40 | 4.35 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 251 frames at 120 Hz; the text stood still on 0 of them, 256 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.05 | 12.46 | 100% | 0.58 | 2.62 | 4.27 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.49 | 5.66 | 100.60 | 98% | 2.55 | 5.09 | 4.77 | 307.6 → 299.9 | 8 | 1.1 / 4 |

