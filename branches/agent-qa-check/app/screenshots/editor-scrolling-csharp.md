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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.83 | 2.83 | 3.93 | 100% | 0.53 | 2.30 | 3.58 | 87.1 → 88.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.92 | 15.80 | 99% | 0.78 | 2.31 | 3.61 | 125.4 → 123.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 2.64 | 16.11 | 99% | 0.71 | 2.13 | 3.05 | 161.6 → 145.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 4.41 | 73.22 | 99% | 2.30 | 2.45 | 3.27 | 104.8 → 101.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.28 | 10.64 | 100% | 0.47 | 1.92 | 2.75 | 81.9 → 74.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.51 | 18.32 | 100% | 0.55 | 2.05 | 3.09 | 107.5 → 99.0 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 2.43 | 13.70 | 99% | 0.46 | 2.04 | 2.93 | 107.4 → 99.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.56 | 14.07 | 99% | 0.77 | 2.16 | 5.42 | 106.7 → 98.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.44 | 9.83 | 100% | 0.44 | 2.05 | 2.88 | 84.3 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 4.31 | 15.96 | 99% | 2.57 | 2.39 | 3.58 | 110.0 → 101.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.53 | 13.58 | 99% | 0.52 | 2.10 | 2.87 | 110.1 → 101.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.17 | 15.15 | 100% | 0.49 | 1.79 | 2.10 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.206 |
| AttachingStyle - | 0.048 |
| FindingResource - | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.52 | 2.62 | 10.71 | 98% | 0.41 | 2.05 | 2.99 | 50.4 → 73.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 11 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.65 | 32.79 | 99% | 0.58 | 2.16 | 3.44 | 105.7 → 102.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.30 | 5.45 | 19.66 | 98% | 1.68 | 4.72 | 3.71 | 320.5 → 312.3 | 7 | 1.1 / 4 |

