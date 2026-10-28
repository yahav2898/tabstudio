# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 2.83 | 20.49 | 100% | 0.50 | 2.36 | 5.28 | 86.1 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 4.12 | 20.45 | 99% | 1.41 | 2.56 | 5.52 | 123.4 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 2.93 | 19.55 | 99% | 0.92 | 2.31 | 5.19 | 158.6 → 142.9 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.88 | 19.89 | 99% | 0.63 | 2.33 | 5.27 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.70 | 18.62 | 100% | 0.62 | 2.17 | 4.94 | 80.6 → 73.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 3.21 | 18.90 | 100% | 1.22 | 2.41 | 5.43 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 2.78 | 19.85 | 100% | 0.63 | 2.27 | 5.06 | 105.7 → 97.4 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.81 | 19.83 | 99% | 0.61 | 2.30 | 5.06 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 3.26 | 72.11 | 99% | 0.63 | 2.51 | 5.30 | 83.4 → 78.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 2.93 | 18.54 | 99% | 0.61 | 2.37 | 5.21 | 108.4 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.74 | 18.61 | 100% | 0.60 | 2.23 | 4.41 | 108.4 → 100.2 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.38 | 17.62 | 100% | 0.60 | 1.95 | 3.73 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| AttachingStyle - | 0.101 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 3.33 | 4.74 | 100% | 0.53 | 2.56 | 5.20 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.89 | 18.87 | 100% | 0.59 | 2.35 | 5.18 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.47 | 5.36 | 71.63 | 98% | 1.52 | 4.82 | 5.62 | 316.6 → 308.6 | 6 | 1.1 / 4 |

