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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.03 | 44.98 | 100% | 0.37 | 1.81 | 4.18 | 85.0 → 86.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 2.34 | 4.36 | 100% | 0.58 | 1.86 | 4.31 | 122.4 → 122.8 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 3.14 | 50.75 | 100% | 1.22 | 2.35 | 4.45 | 158.4 → 143.2 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 2.13 | 3.04 | 100% | 0.49 | 1.74 | 4.07 | 102.4 → 99.6 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.04 | 44.40 | 100% | 0.53 | 1.68 | 3.98 | 80.2 → 73.4 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.57 | 2.20 | 3.97 | 100% | 0.53 | 1.79 | 4.27 | 105.2 → 96.9 | 0 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 2.85 | 9.82 | 100% | 1.00 | 2.29 | 4.43 | 105.0 → 96.9 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.55 | 2.12 | 2.74 | 100% | 0.51 | 1.74 | 4.15 | 104.4 → 96.2 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.55 | 2.12 | 2.90 | 100% | 0.45 | 1.75 | 4.03 | 82.4 → 77.1 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.60 | 2.14 | 41.63 | 100% | 0.53 | 1.79 | 4.28 | 107.6 → 99.6 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.61 | 2.73 | 5.03 | 100% | 0.92 | 1.91 | 3.81 | 107.7 → 99.5 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.02 | 9.21 | 100% | 0.82 | 1.57 | 3.00 | 45.7 → 42.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 61.3

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 2.13 | 2.88 | 100% | 0.36 | 1.70 | 4.73 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 74 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.84 | 90.95 | 100% | 1.00 | 2.31 | 4.66 | 103.4 → 100.2 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.91 | 2.55 | 41.98 | 100% | 0.58 | 1.99 | 4.58 | 308.2 → 300.4 | 2 | 1.1 / 4 |

