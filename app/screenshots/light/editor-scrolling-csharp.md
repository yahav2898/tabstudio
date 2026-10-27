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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.04 | 11.31 | 99% | 0.35 | 1.80 | 4.27 | 121.6 → 123.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 4.03 | 18.31 | 99% | 0.82 | 2.60 | 4.70 | 159.8 → 158.2 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.41 | 17.11 | 99% | 0.74 | 1.89 | 4.54 | 194.4 → 179.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.20 | 12.61 | 99% | 0.50 | 1.82 | 4.30 | 139.4 → 136.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.08 | 11.74 | 99% | 0.51 | 1.70 | 4.71 | 116.3 → 109.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 2.19 | 17.26 | 99% | 0.52 | 1.80 | 4.28 | 141.4 → 133.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 3.00 | 14.79 | 99% | 0.64 | 2.45 | 4.67 | 141.7 → 134.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.75 | 14.44 | 99% | 0.54 | 2.04 | 4.43 | 140.8 → 133.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.05 | 11.31 | 99% | 0.43 | 1.73 | 4.03 | 119.0 → 114.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.16 | 12.31 | 99% | 0.50 | 1.78 | 4.28 | 143.9 → 136.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 2.15 | 13.44 | 99% | 0.51 | 1.81 | 3.85 | 143.9 → 136.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.99 | 9.64 | 100% | 0.45 | 1.61 | 3.29 | 79.9 → 77.1 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.320 |
| AttachingStyle - | 0.098 |
| MeasuringLayoutable CachedLineNumberMargin | 0.044 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| FindingResource - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable ScrollContentPresenter | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.76 | 2.43 | 4.04 | 100% | 0.44 | 1.90 | 4.78 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 57 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.20 | 12.10 | 100% | 0.49 | 1.86 | 4.35 | 139.8 → 137.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.63 | 17.21 | 98% | 0.63 | 2.12 | 4.39 | 350.8 → 344.4 | 8 | 1.1 / 4 |

