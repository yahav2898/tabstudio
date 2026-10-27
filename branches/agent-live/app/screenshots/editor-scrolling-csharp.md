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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.09 | 11.43 | 99% | 0.34 | 1.84 | 4.20 | 122.2 → 123.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.53 | 13.53 | 99% | 0.57 | 2.03 | 4.33 | 160.2 → 157.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 3.22 | 15.93 | 99% | 0.73 | 2.60 | 4.52 | 196.0 → 180.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 4.21 | 15.04 | 99% | 0.55 | 2.65 | 4.49 | 141.1 → 137.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.64 | 15.46 | 99% | 0.51 | 2.30 | 4.08 | 116.7 → 110.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.58 | 13.16 | 99% | 0.52 | 2.30 | 4.23 | 142.3 → 134.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.13 | 12.47 | 99% | 0.50 | 1.72 | 4.09 | 142.1 → 134.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 4.20 | 13.81 | 99% | 0.70 | 3.02 | 4.56 | 141.9 → 134.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.01 | 11.06 | 99% | 0.42 | 1.67 | 3.90 | 119.4 → 114.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.17 | 12.35 | 99% | 0.52 | 1.76 | 4.15 | 144.7 → 137.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.17 | 13.14 | 99% | 0.49 | 1.79 | 3.63 | 144.7 → 136.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.67 | 7.51 | 100% | 0.40 | 1.42 | 3.03 | 80.3 → 76.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.317 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.011 |
| RaisingRoutedEvent Border | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.82 | 4.48 | 73.42 | 98% | 0.55 | 2.43 | 5.54 | 87.4 → 110.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 123 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.19 | 15.58 | 99% | 0.50 | 1.83 | 4.29 | 140.4 → 137.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.09 | 2.65 | 18.82 | 98% | 0.60 | 2.14 | 4.37 | 352.5 → 346.3 | 8 | 1.1 / 4 |

