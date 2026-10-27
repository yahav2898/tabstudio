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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 4.31 | 12.87 | 99% | 0.58 | 2.84 | 5.27 | 124.3 → 125.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 2.93 | 14.53 | 99% | 0.73 | 2.28 | 5.30 | 162.3 → 159.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.25 | 3.07 | 17.51 | 99% | 0.89 | 2.34 | 5.31 | 198.6 → 182.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 2.67 | 14.36 | 99% | 0.66 | 2.20 | 5.05 | 141.4 → 138.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.89 | 3.34 | 12.60 | 100% | 0.69 | 2.51 | 5.15 | 118.2 → 111.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 2.91 | 13.61 | 99% | 0.72 | 2.32 | 5.36 | 144.3 → 136.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 2.75 | 16.16 | 99% | 0.65 | 2.20 | 5.15 | 143.5 → 135.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 2.75 | 15.58 | 99% | 0.62 | 2.17 | 4.97 | 142.8 → 134.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 3.30 | 85.89 | 99% | 0.70 | 2.53 | 5.09 | 120.6 → 116.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.96 | 2.79 | 13.90 | 99% | 0.64 | 2.16 | 5.05 | 146.3 → 138.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 2.88 | 12.68 | 99% | 0.72 | 2.25 | 4.53 | 145.9 → 138.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.26 | 9.54 | 100% | 0.54 | 1.83 | 3.70 | 81.5 → 78.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.228 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.11 | 3.27 | 4.41 | 100% | 2.26 | 2.27 | 5.62 | 88.9 → 110.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 198 frames at 120 Hz; the text stood still on 0 of them, 233 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 2.99 | 13.41 | 99% | 0.68 | 2.33 | 5.22 | 142.2 → 139.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.62 | 3.59 | 19.15 | 98% | 0.80 | 2.77 | 5.32 | 356.9 → 350.2 | 8 | 1.1 / 4 |

