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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 1.33 | 8.09 | 100% | 0.28 | 1.12 | 2.79 | 112.0 → 113.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 1.37 | 9.70 | 99% | 0.34 | 1.11 | 2.55 | 150.6 → 148.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.47 | 1.43 | 10.78 | 99% | 0.45 | 1.10 | 2.52 | 186.6 → 170.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 1.27 | 7.75 | 100% | 0.33 | 1.06 | 2.45 | 129.8 → 127.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 2.47 | 13.03 | 99% | 0.43 | 1.14 | 2.56 | 106.9 → 100.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.46 | 2.41 | 10.54 | 100% | 0.43 | 1.42 | 3.06 | 133.4 → 124.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 1.28 | 8.22 | 99% | 0.32 | 1.04 | 2.38 | 132.1 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.24 | 8.41 | 100% | 0.32 | 1.03 | 2.35 | 131.5 → 123.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 1.21 | 7.53 | 100% | 0.26 | 1.03 | 2.27 | 109.1 → 104.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.36 | 1.27 | 8.30 | 100% | 0.32 | 1.05 | 2.42 | 134.9 → 127.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 1.20 | 9.45 | 99% | 0.29 | 1.02 | 2.11 | 134.7 → 126.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.26 | 1.55 | 7.75 | 100% | 0.38 | 0.97 | 1.90 | 70.5 → 67.1 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.239 |
| AttachingStyle - | 0.074 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.28 | 1.26 | 1.39 | 100% | 0.26 | 1.02 | 2.54 | 76.4 → 98.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 0 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.37 | 1.35 | 8.22 | 100% | 0.32 | 1.09 | 2.89 | 130.6 → 127.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.58 | 1.54 | 11.69 | 98% | 0.36 | 1.24 | 2.54 | 344.0 → 337.5 | 8 | 1.1 / 4 |

