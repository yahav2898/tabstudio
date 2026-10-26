# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 2.22 | 10.81 | 99% | 0.47 | 1.82 | 4.65 | 113.5 → 114.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.43 | 16.03 | 99% | 0.66 | 2.63 | 4.64 | 151.5 → 150.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.31 | 12.77 | 99% | 0.73 | 1.73 | 4.25 | 186.9 → 171.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.26 | 10.63 | 99% | 0.58 | 1.86 | 4.44 | 131.1 → 128.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.12 | 8.60 | 100% | 0.58 | 1.68 | 4.30 | 107.6 → 100.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 3.37 | 10.87 | 99% | 1.05 | 2.25 | 5.01 | 133.9 → 126.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.12 | 10.27 | 99% | 0.56 | 1.70 | 4.22 | 133.3 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 2.74 | 11.33 | 99% | 0.54 | 2.34 | 4.10 | 132.8 → 124.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.46 | 9.24 | 99% | 0.44 | 2.23 | 3.90 | 110.5 → 105.8 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.90 | 4.42 | 11.00 | 99% | 0.67 | 2.76 | 4.95 | 137.1 → 128.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.91 | 11.24 | 99% | 0.62 | 2.43 | 4.16 | 136.0 → 128.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.18 | 8.45 | 100% | 0.57 | 1.71 | 3.63 | 72.4 → 69.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.287 |
| ArrangingLayoutable TextView | 0.060 |
| ArrangingLayoutable LineDrawing | 0.051 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.02 | 3.15 | 4.69 | 100% | 0.54 | 2.53 | 5.16 | 79.3 → 101.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 225 frames at 120 Hz; the text stood still on 0 of them, 258 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 2.86 | 13.04 | 99% | 0.60 | 2.39 | 4.60 | 131.9 → 129.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 3.48 | 17.18 | 98% | 0.74 | 2.87 | 4.90 | 345.4 → 338.9 | 8 | 1.1 / 4 |

