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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.79 | 14.80 | 100% | 0.46 | 2.41 | 3.80 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.06 | 16.94 | 99% | 0.66 | 2.53 | 3.86 | 121.3 → 121.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.09 | 17.04 | 100% | 0.87 | 2.52 | 4.07 | 156.8 → 141.8 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 3.22 | 21.19 | 99% | 1.55 | 2.57 | 4.02 | 101.6 → 98.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.74 | 11.43 | 100% | 0.60 | 2.29 | 3.61 | 79.7 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.65 | 2.86 | 18.26 | 99% | 0.60 | 2.42 | 3.83 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.69 | 2.92 | 17.26 | 100% | 0.55 | 2.44 | 3.81 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.81 | 14.10 | 100% | 0.54 | 2.37 | 3.62 | 103.5 → 95.3 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.65 | 2.89 | 15.50 | 99% | 0.48 | 2.47 | 3.72 | 82.3 → 76.9 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.79 | 4.85 | 21.78 | 100% | 1.65 | 2.58 | 4.11 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.64 | 2.92 | 16.21 | 99% | 0.58 | 2.43 | 3.36 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.56 | 11.15 | 100% | 0.56 | 2.18 | 2.84 | 46.1 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| AttachingStyle - | 0.065 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 2.77 | 3.03 | 100% | 0.47 | 2.29 | 4.02 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 220 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.26 | 15.50 | 99% | 0.63 | 2.56 | 3.97 | 102.6 → 99.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 3.30 | 18.68 | 98% | 0.68 | 2.70 | 3.96 | 307.6 → 299.8 | 7 | 1.1 / 4 |

