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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.55 | 25.05 | 100% | 0.37 | 2.04 | 4.31 | 86.4 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 2.37 | 3.13 | 100% | 0.61 | 1.90 | 4.45 | 124.2 → 121.9 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 2.48 | 44.93 | 100% | 0.81 | 1.93 | 4.74 | 159.9 → 144.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.27 | 3.63 | 100% | 0.53 | 1.88 | 4.44 | 103.9 → 100.9 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.32 | 47.85 | 100% | 0.58 | 1.80 | 4.29 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.61 | 2.98 | 76.47 | 100% | 0.83 | 1.95 | 4.38 | 106.5 → 98.1 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.28 | 57.79 | 100% | 0.52 | 1.86 | 4.44 | 106.4 → 98.0 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.23 | 4.21 | 100% | 0.54 | 1.85 | 4.37 | 105.7 → 97.4 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.16 | 2.90 | 100% | 0.42 | 1.81 | 4.16 | 83.8 → 78.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.67 | 43.72 | 100% | 0.66 | 2.02 | 4.50 | 109.1 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.92 | 5.46 | 100% | 2.23 | 2.29 | 3.93 | 109.2 → 100.8 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.94 | 27.96 | 100% | 0.89 | 1.61 | 3.14 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.180 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 2.12 | 2.25 | 100% | 0.35 | 1.71 | 4.39 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 41 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 2.23 | 39.65 | 100% | 0.48 | 1.85 | 4.37 | 104.8 → 101.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 4.70 | 80.68 | 99% | 2.40 | 4.16 | 4.59 | 318.4 → 310.2 | 2 | 1.1 / 4 |

