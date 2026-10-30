# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 2.50 | 14.52 | 100% | 0.39 | 1.92 | 4.29 | 86.7 → 87.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 2.30 | 3.14 | 100% | 0.62 | 1.83 | 4.27 | 124.5 → 122.3 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 2.42 | 45.56 | 100% | 0.78 | 1.83 | 4.32 | 160.2 → 144.4 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.14 | 2.92 | 100% | 0.48 | 1.77 | 4.00 | 104.1 → 101.2 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.07 | 46.54 | 100% | 0.53 | 1.63 | 3.80 | 81.1 → 73.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.82 | 5.14 | 100% | 1.19 | 2.32 | 4.16 | 106.8 → 98.4 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 2.12 | 49.97 | 100% | 0.53 | 1.74 | 4.07 | 106.7 → 98.3 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 2.10 | 2.61 | 100% | 0.48 | 1.74 | 4.10 | 105.9 → 97.6 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 2.01 | 37.72 | 100% | 0.40 | 1.67 | 3.71 | 83.9 → 78.5 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 2.18 | 4.19 | 100% | 0.49 | 1.79 | 4.26 | 109.3 → 101.2 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.86 | 11.77 | 100% | 1.12 | 2.35 | 3.55 | 109.4 → 101.1 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.90 | 4.06 | 100% | 0.74 | 1.52 | 3.39 | 46.1 → 42.3 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.178 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 1.83 | 1.93 | 100% | 0.31 | 1.58 | 3.93 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 43 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.57 | 2.09 | 2.41 | 100% | 0.46 | 1.73 | 4.06 | 105.0 → 101.8 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.29 | 4.61 | 41.96 | 99% | 2.44 | 4.02 | 4.45 | 318.6 → 310.6 | 3 | 1.1 / 4 |

