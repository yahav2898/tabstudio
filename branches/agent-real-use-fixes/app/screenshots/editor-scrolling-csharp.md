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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.40 | 11.50 | 99% | 0.43 | 1.99 | 4.51 | 122.3 → 123.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 2.69 | 15.61 | 99% | 0.63 | 2.15 | 4.82 | 160.3 → 158.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 4.67 | 16.76 | 99% | 1.26 | 2.70 | 5.01 | 197.5 → 180.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.27 | 11.63 | 99% | 0.53 | 1.84 | 4.28 | 139.9 → 137.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.19 | 9.19 | 99% | 0.51 | 1.73 | 4.17 | 116.8 → 110.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.39 | 12.35 | 99% | 0.59 | 1.91 | 4.53 | 142.3 → 134.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.88 | 4.18 | 17.00 | 99% | 0.69 | 2.45 | 4.69 | 142.8 → 134.8 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.25 | 10.58 | 99% | 0.49 | 1.80 | 4.24 | 141.4 → 133.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.25 | 10.31 | 99% | 0.50 | 1.85 | 4.18 | 119.4 → 114.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.82 | 12.27 | 99% | 0.56 | 2.47 | 4.41 | 144.8 → 137.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 3.23 | 11.82 | 99% | 0.58 | 2.61 | 4.09 | 145.0 → 137.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.79 | 11.48 | 100% | 0.53 | 2.42 | 3.62 | 81.8 → 77.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.230 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.62 | 2.73 | 8.43 | 98% | 0.40 | 2.42 | 4.39 | 87.4 → 109.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 252 frames at 120 Hz; the text stood still on 0 of them, 254 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.15 | 11.89 | 99% | 0.62 | 2.69 | 4.74 | 141.3 → 137.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 5.59 | 80.36 | 98% | 2.43 | 5.02 | 4.87 | 354.5 → 347.5 | 9 | 1.1 / 4 |

