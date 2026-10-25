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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.11 | 9.40 | 99% | 0.37 | 1.83 | 3.43 | 110.7 → 112.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 2.17 | 30.56 | 99% | 0.49 | 1.83 | 3.68 | 149.8 → 147.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 2.66 | 10.58 | 99% | 0.62 | 2.10 | 3.51 | 185.6 → 169.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 2.90 | 65.46 | 99% | 0.46 | 2.31 | 3.52 | 129.4 → 125.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.88 | 7.17 | 100% | 0.40 | 1.65 | 3.17 | 105.1 → 98.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.03 | 12.11 | 99% | 0.46 | 1.75 | 3.42 | 131.7 → 123.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 2.15 | 8.75 | 99% | 0.46 | 1.85 | 3.38 | 131.4 → 123.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.08 | 14.07 | 99% | 0.68 | 2.43 | 3.64 | 130.9 → 123.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.62 | 7.49 | 100% | 0.38 | 1.33 | 3.21 | 107.7 → 102.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.71 | 12.83 | 99% | 0.45 | 1.35 | 3.38 | 133.4 → 125.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 1.67 | 9.64 | 99% | 0.45 | 1.34 | 3.03 | 133.5 → 125.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 1.38 | 6.44 | 100% | 0.37 | 1.11 | 2.54 | 68.9 → 65.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.186 |
| AttachingStyle - | 0.039 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 3.82 | 13.57 | 98% | 0.41 | 3.41 | 4.56 | 76.2 → 97.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 50 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.50 | 1.47 | 9.27 | 99% | 0.40 | 1.25 | 3.20 | 129.1 → 126.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.85 | 2.16 | 13.22 | 98% | 0.54 | 1.68 | 3.46 | 342.7 → 336.4 | 8 | 1.1 / 4 |

