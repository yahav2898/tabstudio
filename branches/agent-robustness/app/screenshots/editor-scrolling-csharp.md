# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 2.16 | 14.02 | 99% | 0.34 | 1.87 | 2.70 | 87.1 → 88.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 2.35 | 13.82 | 99% | 0.47 | 1.93 | 2.76 | 125.4 → 123.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 2.43 | 14.66 | 99% | 0.60 | 1.98 | 2.87 | 161.6 → 145.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.46 | 14.16 | 100% | 0.47 | 2.03 | 2.94 | 104.8 → 101.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.49 | 11.79 | 100% | 0.50 | 2.01 | 2.80 | 81.9 → 74.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 2.96 | 4.82 | 100% | 1.48 | 2.33 | 3.40 | 107.5 → 99.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.41 | 14.99 | 99% | 0.47 | 2.01 | 2.96 | 107.4 → 99.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 2.32 | 14.65 | 99% | 0.46 | 1.95 | 2.75 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.27 | 12.24 | 99% | 0.39 | 1.94 | 2.72 | 84.3 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 2.36 | 13.68 | 99% | 0.45 | 1.98 | 2.79 | 110.0 → 101.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.52 | 2.32 | 12.02 | 99% | 0.44 | 2.00 | 2.47 | 110.2 → 101.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.09 | 10.44 | 100% | 1.04 | 1.80 | 1.96 | 46.5 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable LineDrawing | 0.121 |
| ArrangingLayoutable TextView | 0.033 |
| AttachingStyle - | 0.023 |
| ArrangingLayoutable CachedLineNumberMargin | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| RaisingRoutedEvent ScrollViewer | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.78 | 2.88 | 4.63 | 100% | 0.47 | 2.44 | 3.42 | 50.6 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 47 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.57 | 2.39 | 11.80 | 99% | 0.45 | 1.99 | 2.80 | 105.7 → 102.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.76 | 2.55 | 20.54 | 98% | 0.49 | 2.15 | 2.88 | 320.4 → 312.3 | 7 | 1.1 / 4 |

