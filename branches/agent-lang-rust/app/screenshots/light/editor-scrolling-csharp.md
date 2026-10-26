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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.28 | 8.88 | 99% | 0.24 | 1.08 | 2.61 | 110.8 → 112.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.47 | 1.49 | 9.97 | 99% | 0.40 | 1.18 | 2.64 | 148.6 → 146.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 1.54 | 12.61 | 99% | 0.45 | 1.17 | 2.62 | 183.7 → 168.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.46 | 1.47 | 9.61 | 99% | 0.38 | 1.21 | 2.76 | 128.4 → 125.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.36 | 1.35 | 8.98 | 100% | 0.37 | 1.12 | 2.70 | 105.2 → 98.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.50 | 2.61 | 15.86 | 99% | 0.47 | 2.24 | 2.67 | 131.1 → 123.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 2.71 | 62.03 | 99% | 0.43 | 2.35 | 3.20 | 131.5 → 122.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.39 | 1.39 | 10.78 | 99% | 0.37 | 1.09 | 2.44 | 129.8 → 122.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.40 | 1.34 | 7.70 | 100% | 0.32 | 1.13 | 2.46 | 108.3 → 103.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.24 | 9.10 | 99% | 0.31 | 1.04 | 2.34 | 133.2 → 125.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.40 | 1.68 | 10.22 | 99% | 0.50 | 1.33 | 2.98 | 133.2 → 125.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 1.16 | 7.57 | 100% | 0.33 | 0.98 | 1.91 | 70.0 → 66.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.214 |
| ArrangingLayoutable TextView | 0.021 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| AttachingStyle - | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.39 | 2.49 | 2.65 | 100% | 0.36 | 1.21 | 2.67 | 76.1 → 97.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 43 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.46 | 1.43 | 10.34 | 99% | 0.37 | 1.16 | 2.60 | 129.1 → 126.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.65 | 1.71 | 13.49 | 98% | 0.41 | 1.37 | 2.70 | 340.6 → 333.5 | 8 | 1.1 / 4 |

