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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.33 | 1.27 | 7.78 | 100% | 0.21 | 1.09 | 2.25 | 117.5 → 118.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 1.38 | 8.76 | 99% | 0.34 | 1.15 | 2.44 | 155.6 → 153.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.45 | 1.46 | 10.57 | 99% | 0.40 | 1.17 | 2.34 | 191.1 → 175.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.43 | 2.57 | 9.60 | 99% | 0.36 | 2.19 | 2.49 | 135.6 → 133.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 3.00 | 10.68 | 99% | 0.49 | 2.24 | 2.75 | 112.8 → 106.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.40 | 1.37 | 12.36 | 99% | 0.36 | 1.14 | 2.42 | 137.8 → 129.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 1.38 | 8.56 | 100% | 0.33 | 1.16 | 2.40 | 137.6 → 129.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.31 | 8.36 | 99% | 0.31 | 1.11 | 2.26 | 136.9 → 128.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 1.27 | 7.93 | 100% | 0.25 | 1.08 | 2.21 | 114.7 → 109.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.37 | 1.34 | 8.39 | 100% | 0.31 | 1.11 | 2.35 | 140.1 → 132.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 1.28 | 8.04 | 100% | 0.29 | 1.09 | 1.95 | 139.6 → 132.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.18 | 7.10 | 100% | 0.28 | 0.99 | 1.67 | 76.3 → 73.0 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| AttachingStyle - | 0.041 |
| FindingResource - | 0.018 |
| ArrangingLayoutable LineDrawing | 0.016 |
| ArrangingLayoutable Track | 0.015 |
| ArrangingLayoutable ScrollingTextLayer | 0.015 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.42 | 2.55 | 3.31 | 100% | 0.30 | 2.20 | 2.53 | 82.6 → 104.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 34 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 1.31 | 8.78 | 100% | 0.30 | 1.10 | 2.29 | 135.9 → 133.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.58 | 1.54 | 12.45 | 98% | 0.36 | 1.29 | 2.36 | 348.0 → 341.6 | 8 | 1.1 / 4 |

