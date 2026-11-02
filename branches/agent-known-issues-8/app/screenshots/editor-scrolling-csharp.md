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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.14 | 13.45 | 100% | 0.46 | 2.72 | 4.47 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 3.52 | 17.22 | 99% | 0.68 | 2.89 | 4.54 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.61 | 18.20 | 99% | 0.99 | 2.86 | 4.59 | 158.2 → 143.1 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.39 | 16.48 | 99% | 0.61 | 2.88 | 4.67 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 4.39 | 31.04 | 99% | 1.71 | 2.82 | 4.70 | 80.5 → 73.7 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.68 | 3.08 | 16.16 | 99% | 0.66 | 2.63 | 4.37 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.73 | 3.14 | 16.89 | 99% | 0.63 | 2.65 | 4.44 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.18 | 15.43 | 99% | 0.68 | 2.70 | 4.37 | 104.3 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.66 | 3.04 | 12.47 | 99% | 0.53 | 2.60 | 4.15 | 82.7 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.84 | 3.60 | 20.39 | 99% | 0.78 | 2.82 | 4.75 | 107.5 → 99.4 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.75 | 3.33 | 15.26 | 99% | 0.60 | 2.79 | 4.13 | 107.6 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.76 | 12.81 | 100% | 0.56 | 2.36 | 3.18 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable ScrollingTextLayer | 0.055 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 3.08 | 3.12 | 100% | 0.42 | 2.51 | 4.38 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 231 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.95 | 3.44 | 19.10 | 99% | 0.83 | 2.88 | 5.01 | 103.3 → 100.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 3.83 | 19.86 | 98% | 0.74 | 3.10 | 4.57 | 308.9 → 301.1 | 7 | 1.1 / 4 |

