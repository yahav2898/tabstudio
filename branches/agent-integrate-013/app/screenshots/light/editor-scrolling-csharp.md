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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 2.09 | 8.53 | 99% | 0.35 | 1.85 | 3.61 | 112.6 → 114.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.02 | 12.57 | 99% | 0.43 | 1.77 | 3.44 | 150.9 → 148.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.36 | 13.27 | 99% | 0.62 | 1.91 | 3.88 | 187.0 → 170.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 2.24 | 11.01 | 99% | 0.43 | 1.86 | 3.52 | 130.4 → 127.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 3.03 | 10.78 | 100% | 0.47 | 2.39 | 3.69 | 107.5 → 100.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 2.01 | 9.92 | 99% | 0.41 | 1.71 | 3.56 | 132.8 → 124.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.15 | 16.08 | 99% | 0.47 | 1.85 | 3.70 | 132.7 → 124.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 1.96 | 9.21 | 99% | 0.45 | 1.59 | 3.59 | 131.9 → 124.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.48 | 1.80 | 8.12 | 100% | 0.41 | 1.37 | 3.44 | 109.4 → 104.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.72 | 12.07 | 99% | 0.45 | 1.50 | 3.75 | 136.1 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 1.65 | 10.10 | 99% | 0.40 | 1.32 | 3.36 | 135.1 → 127.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.36 | 6.17 | 100% | 0.37 | 1.07 | 2.77 | 70.6 → 67.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.201 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 1.85 | 8.92 | 98% | 0.41 | 1.35 | 3.70 | 77.5 → 99.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.62 | 9.46 | 99% | 0.45 | 2.30 | 3.65 | 131.5 → 127.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.85 | 2.01 | 14.49 | 98% | 0.50 | 1.58 | 3.51 | 343.4 → 336.8 | 8 | 1.1 / 4 |

