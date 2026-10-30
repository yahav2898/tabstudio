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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.18 | 12.40 | 100% | 0.45 | 2.77 | 4.51 | 86.7 → 87.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 3.40 | 16.05 | 99% | 0.70 | 2.78 | 4.46 | 124.6 → 122.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 4.09 | 17.64 | 99% | 1.89 | 3.01 | 5.02 | 160.3 → 144.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.32 | 11.90 | 100% | 0.59 | 2.82 | 4.70 | 104.2 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 3.06 | 15.59 | 100% | 0.60 | 2.60 | 4.12 | 81.1 → 73.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 3.27 | 17.58 | 100% | 0.61 | 2.79 | 4.48 | 106.7 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 3.46 | 19.66 | 99% | 0.67 | 2.90 | 4.78 | 106.7 → 98.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.52 | 15.84 | 99% | 0.73 | 2.83 | 4.54 | 106.0 → 97.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.28 | 9.28 | 99% | 0.53 | 2.78 | 4.43 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 3.35 | 13.75 | 99% | 0.62 | 2.84 | 4.73 | 109.3 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 3.29 | 15.38 | 99% | 0.67 | 2.84 | 4.05 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.93 | 13.88 | 100% | 1.15 | 2.50 | 3.58 | 46.2 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.200 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 3.12 | 12.36 | 98% | 0.44 | 2.63 | 4.35 | 50.5 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 243 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.25 | 12.71 | 99% | 0.57 | 2.80 | 4.46 | 105.1 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.42 | 5.93 | 24.72 | 98% | 2.44 | 5.19 | 5.04 | 318.8 → 310.7 | 8 | 1.1 / 4 |

