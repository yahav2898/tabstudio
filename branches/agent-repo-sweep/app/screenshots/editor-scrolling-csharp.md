# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.97 | 36.93 | 99% | 0.46 | 2.57 | 5.68 | 86.6 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 4.29 | 17.41 | 100% | 1.52 | 2.77 | 6.20 | 124.3 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.60 | 18.50 | 99% | 1.60 | 2.78 | 6.45 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.98 | 14.75 | 99% | 0.57 | 2.47 | 5.65 | 104.0 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.83 | 13.32 | 100% | 0.56 | 2.35 | 5.52 | 81.4 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 3.24 | 18.97 | 99% | 1.14 | 2.57 | 5.69 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.80 | 3.06 | 15.72 | 99% | 0.57 | 2.58 | 5.82 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.92 | 16.10 | 100% | 0.52 | 2.43 | 5.42 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 2.92 | 13.50 | 99% | 0.53 | 2.44 | 5.42 | 83.9 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 4.53 | 80.64 | 99% | 1.72 | 2.62 | 5.86 | 109.2 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 3.00 | 16.01 | 99% | 0.55 | 2.53 | 5.15 | 109.3 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.69 | 14.34 | 100% | 0.54 | 2.24 | 4.57 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.276 |
| ArrangingLayoutable LineDrawing | 0.061 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| touch drag and flick (real touch input) | 46 | 0.77 | 3.09 | 3.31 | 100% | 0.48 | 2.57 | 6.00 | 50.3 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 160 frames at 120 Hz; the text stood still on 0 of them, 195 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.05 | 13.71 | 99% | 0.54 | 2.54 | 5.56 | 104.9 → 101.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 3.31 | 18.91 | 98% | 0.64 | 2.74 | 5.57 | 318.4 → 310.4 | 7 | 1.1 / 4 |

