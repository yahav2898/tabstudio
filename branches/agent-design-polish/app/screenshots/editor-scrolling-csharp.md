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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.10 | 2.98 | 15.22 | 99% | 0.59 | 2.40 | 5.91 | 121.5 → 122.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.27 | 3.43 | 17.54 | 99% | 0.89 | 2.65 | 5.82 | 158.9 → 157.6 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 4.18 | 18.49 | 99% | 1.09 | 2.85 | 6.04 | 195.4 → 178.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.22 | 3.23 | 15.97 | 99% | 0.79 | 2.54 | 5.88 | 139.6 → 136.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 2.86 | 13.99 | 99% | 0.68 | 2.21 | 5.53 | 116.1 → 107.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.20 | 3.19 | 17.34 | 99% | 0.90 | 2.43 | 5.85 | 141.3 → 134.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.15 | 3.58 | 18.93 | 99% | 1.04 | 2.63 | 5.91 | 141.5 → 132.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.18 | 3.00 | 17.37 | 99% | 0.75 | 2.31 | 5.78 | 140.5 → 132.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.21 | 3.11 | 14.99 | 99% | 0.75 | 2.48 | 5.82 | 119.4 → 114.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.07 | 2.99 | 16.56 | 99% | 0.75 | 2.35 | 5.79 | 143.5 → 136.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.30 | 3.59 | 19.64 | 99% | 0.84 | 2.78 | 5.42 | 144.1 → 136.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.43 | 14.99 | 99% | 0.56 | 1.97 | 4.37 | 79.3 → 75.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.259 |
| ArrangingLayoutable TextView | 0.076 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.98 | 2.70 | 2.88 | 100% | 0.59 | 2.10 | 5.96 | 86.3 → 107.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 220 frames at 120 Hz; the text stood still on 0 of them, 253 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.15 | 3.31 | 15.82 | 99% | 0.73 | 2.54 | 5.89 | 139.0 → 137.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.65 | 4.01 | 21.73 | 98% | 0.86 | 3.17 | 5.85 | 352.5 → 344.9 | 8 | 1.1 / 4 |

