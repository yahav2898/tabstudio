# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.70 | 13.77 | 99% | 0.51 | 2.25 | 5.05 | 118.1 → 119.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.25 | 4.35 | 14.40 | 99% | 0.77 | 2.70 | 5.47 | 156.9 → 155.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 3.23 | 16.21 | 99% | 0.92 | 2.38 | 5.31 | 192.7 → 177.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.05 | 2.84 | 13.31 | 99% | 0.69 | 2.24 | 5.13 | 136.4 → 133.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.59 | 14.22 | 99% | 0.68 | 2.01 | 4.97 | 112.7 → 106.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.14 | 3.54 | 86.00 | 99% | 0.84 | 2.62 | 5.47 | 139.0 → 131.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 2.90 | 14.61 | 99% | 0.71 | 2.34 | 5.25 | 138.8 → 130.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 2.90 | 14.32 | 99% | 0.72 | 2.26 | 5.08 | 137.8 → 129.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 2.70 | 11.81 | 99% | 0.60 | 2.17 | 4.84 | 115.2 → 110.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.20 | 4.43 | 16.32 | 99% | 0.74 | 2.79 | 5.46 | 141.8 → 134.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 2.86 | 16.91 | 99% | 0.69 | 2.24 | 4.56 | 140.6 → 132.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.33 | 11.45 | 99% | 0.58 | 1.84 | 3.88 | 77.1 → 73.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.334 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.86 | 2.75 | 2.92 | 100% | 0.51 | 2.09 | 5.24 | 82.8 → 104.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 226 frames at 120 Hz; the text stood still on 0 of them, 256 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 3.21 | 12.98 | 99% | 0.65 | 2.68 | 5.38 | 137.5 → 133.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.41 | 3.17 | 18.13 | 98% | 0.75 | 2.49 | 4.96 | 349.8 → 343.5 | 8 | 1.1 / 4 |

