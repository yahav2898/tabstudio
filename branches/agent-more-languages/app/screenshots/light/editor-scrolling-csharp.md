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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 3.04 | 10.18 | 99% | 0.37 | 2.74 | 4.15 | 122.3 → 123.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.21 | 12.34 | 99% | 0.58 | 2.79 | 4.24 | 160.1 → 157.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 3.52 | 14.29 | 99% | 0.70 | 2.84 | 4.31 | 195.5 → 180.2 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.82 | 80.39 | 99% | 0.64 | 3.21 | 4.92 | 141.4 → 137.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.99 | 11.39 | 99% | 0.48 | 2.63 | 3.94 | 116.6 → 110.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 3.09 | 15.06 | 99% | 0.53 | 2.75 | 4.12 | 142.2 → 134.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 3.33 | 12.06 | 99% | 0.54 | 2.85 | 4.42 | 142.6 → 134.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 3.95 | 12.25 | 99% | 0.63 | 3.12 | 5.16 | 143.1 → 133.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.96 | 10.88 | 99% | 0.43 | 2.66 | 3.96 | 119.8 → 114.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 3.17 | 12.08 | 99% | 0.50 | 2.79 | 4.24 | 144.8 → 137.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 3.31 | 12.90 | 99% | 0.53 | 2.89 | 3.86 | 145.3 → 137.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 3.04 | 81.59 | 100% | 0.49 | 2.71 | 3.64 | 82.1 → 78.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 2.97 | 3.07 | 100% | 0.38 | 2.66 | 4.21 | 88.1 → 109.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 249 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.19 | 12.26 | 99% | 0.51 | 2.79 | 4.23 | 140.3 → 138.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.28 | 5.89 | 19.91 | 97% | 2.34 | 5.36 | 4.96 | 354.3 → 346.1 | 8 | 1.1 / 4 |

