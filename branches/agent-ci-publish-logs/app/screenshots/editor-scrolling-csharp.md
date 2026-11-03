# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.24 | 14.93 | 100% | 0.48 | 2.83 | 4.71 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.08 | 3.66 | 18.41 | 99% | 0.82 | 3.06 | 5.13 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.35 | 5.03 | 33.84 | 99% | 2.33 | 3.19 | 5.39 | 158.2 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 3.25 | 12.81 | 100% | 0.57 | 2.72 | 4.45 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.06 | 14.39 | 100% | 0.60 | 2.58 | 4.26 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 3.33 | 21.74 | 99% | 0.65 | 2.73 | 4.59 | 105.1 → 96.6 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.32 | 18.11 | 100% | 0.68 | 2.74 | 4.63 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.97 | 18.70 | 99% | 0.86 | 2.87 | 5.29 | 104.3 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 3.18 | 4.31 | 100% | 0.52 | 2.73 | 4.28 | 82.6 → 77.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.42 | 14.24 | 99% | 0.65 | 2.81 | 4.50 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.00 | 3.70 | 18.31 | 99% | 0.78 | 3.01 | 4.50 | 107.6 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 3.08 | 13.17 | 100% | 1.47 | 2.55 | 3.65 | 46.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| FindingResource - | 0.453 |
| MeasuringLayoutable TextView | 0.338 |
| AttachingStyle - | 0.062 |
| ArrangingLayoutable TextView | 0.030 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.74 | 3.10 | 3.35 | 100% | 0.43 | 2.65 | 4.62 | 49.4 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 219 frames at 120 Hz; the text stood still on 0 of them, 251 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.39 | 21.91 | 99% | 0.63 | 2.86 | 4.66 | 103.3 → 100.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.86 | 6.19 | 24.28 | 98% | 2.64 | 5.40 | 5.23 | 309.0 → 301.1 | 8 | 1.1 / 4 |

