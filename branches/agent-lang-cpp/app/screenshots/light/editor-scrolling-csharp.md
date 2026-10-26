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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.23 | 1.35 | 100% | 0.26 | 0.98 | 2.93 | 112.1 → 113.5 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.37 | 37.63 | 100% | 0.41 | 1.04 | 3.03 | 150.1 → 148.0 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 1.46 | 1.90 | 100% | 0.48 | 1.06 | 3.09 | 185.7 → 169.9 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.49 | 1.31 | 35.12 | 100% | 0.36 | 1.02 | 2.98 | 129.9 → 127.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 2.58 | 9.22 | 100% | 0.41 | 2.25 | 3.07 | 106.3 → 99.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.47 | 1.34 | 3.22 | 100% | 0.36 | 1.02 | 2.98 | 132.1 → 124.1 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 1.29 | 35.34 | 100% | 0.36 | 1.01 | 2.97 | 132.1 → 124.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.48 | 1.24 | 1.57 | 100% | 0.36 | 0.99 | 2.92 | 131.2 → 123.3 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.24 | 38.33 | 100% | 0.31 | 1.00 | 2.88 | 109.1 → 104.1 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 1.27 | 2.29 | 100% | 0.36 | 1.00 | 2.99 | 135.0 → 127.0 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 3.03 | 37.14 | 100% | 2.14 | 2.43 | 2.77 | 134.7 → 127.6 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.09 | 5.51 | 100% | 0.30 | 0.86 | 2.25 | 70.7 → 66.8 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.147 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.42 | 1.28 | 1.46 | 100% | 0.29 | 1.02 | 3.43 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.30 | 36.63 | 100% | 0.36 | 1.01 | 2.99 | 130.6 → 127.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.88 | 3.64 | 42.40 | 99% | 2.34 | 3.30 | 3.15 | 344.4 → 337.2 | 3 | 1.1 / 4 |

