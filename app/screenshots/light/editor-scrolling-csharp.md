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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.19 | 12.71 | 100% | 0.41 | 2.82 | 6.28 | 86.1 → 87.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.40 | 15.66 | 99% | 0.67 | 2.87 | 6.37 | 123.5 → 121.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 4.21 | 16.88 | 99% | 1.84 | 3.06 | 6.95 | 158.6 → 143.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.37 | 78.98 | 99% | 0.60 | 2.87 | 6.61 | 103.4 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.14 | 12.78 | 99% | 0.58 | 2.68 | 6.17 | 80.7 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 3.34 | 14.92 | 99% | 0.59 | 2.82 | 6.36 | 105.8 → 97.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 3.66 | 16.74 | 99% | 0.99 | 2.98 | 7.07 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.30 | 18.39 | 99% | 0.58 | 2.84 | 6.25 | 105.1 → 96.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 3.21 | 4.77 | 100% | 0.47 | 2.77 | 6.18 | 83.5 → 78.1 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 5.07 | 14.58 | 99% | 1.85 | 3.13 | 7.14 | 108.4 → 100.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.36 | 13.11 | 99% | 0.59 | 2.85 | 5.83 | 108.5 → 100.3 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.92 | 4.46 | 100% | 0.64 | 2.56 | 5.26 | 46.1 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.262 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.65 | 3.29 | 3.64 | 100% | 0.46 | 2.82 | 6.58 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2251 ms over 145 frames at 120 Hz; the text stood still on 0 of them, 180 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.43 | 15.96 | 99% | 0.62 | 2.89 | 6.66 | 104.3 → 101.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 3.79 | 17.95 | 98% | 0.72 | 3.16 | 6.48 | 316.7 → 308.6 | 7 | 1.1 / 4 |

