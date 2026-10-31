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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.81 | 2.95 | 11.08 | 99% | 0.52 | 2.45 | 3.65 | 85.1 → 85.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 2.97 | 17.86 | 100% | 0.65 | 2.35 | 3.36 | 122.4 → 122.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.13 | 15.49 | 99% | 0.90 | 2.37 | 3.55 | 158.4 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 2.82 | 15.94 | 99% | 0.61 | 2.26 | 3.29 | 102.4 → 99.6 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.59 | 15.89 | 99% | 0.58 | 2.08 | 3.04 | 80.2 → 73.4 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.85 | 2.82 | 18.57 | 99% | 0.66 | 2.21 | 3.35 | 105.2 → 96.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.92 | 4.51 | 91.44 | 99% | 1.67 | 2.45 | 3.52 | 105.1 → 96.8 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.79 | 15.09 | 99% | 0.61 | 2.24 | 3.29 | 104.4 → 96.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.70 | 2.65 | 14.63 | 99% | 0.52 | 2.16 | 2.97 | 82.7 → 77.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.74 | 2.69 | 15.51 | 100% | 0.56 | 2.21 | 3.19 | 107.6 → 99.6 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.72 | 2.70 | 14.01 | 100% | 0.55 | 2.18 | 2.77 | 107.7 → 99.5 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.38 | 11.48 | 100% | 1.05 | 1.95 | 2.26 | 46.2 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 2.91 | 2.99 | 100% | 0.60 | 2.09 | 3.25 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 263 frames at 120 Hz; the text stood still on 0 of them, 239 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 2.72 | 22.50 | 99% | 0.62 | 2.15 | 3.25 | 103.5 → 100.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 3.13 | 19.74 | 98% | 0.70 | 2.46 | 3.41 | 309.6 → 301.7 | 7 | 1.1 / 4 |

