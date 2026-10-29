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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 2.38 | 8.80 | 100% | 0.30 | 2.14 | 3.27 | 87.1 → 88.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 2.97 | 16.16 | 99% | 0.63 | 2.45 | 3.73 | 125.3 → 123.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 3.99 | 97.28 | 99% | 1.23 | 2.48 | 3.97 | 161.6 → 145.6 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 2.58 | 15.71 | 99% | 0.46 | 2.24 | 3.49 | 104.8 → 101.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.54 | 9.16 | 99% | 0.52 | 2.19 | 3.47 | 81.8 → 74.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.47 | 2.48 | 13.36 | 99% | 0.44 | 2.15 | 3.33 | 107.4 → 99.0 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 2.47 | 14.51 | 99% | 0.43 | 2.16 | 3.33 | 107.4 → 98.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.08 | 13.47 | 99% | 0.62 | 2.54 | 3.79 | 106.7 → 98.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.72 | 12.34 | 99% | 0.44 | 2.34 | 3.49 | 84.3 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 2.72 | 11.51 | 99% | 0.49 | 2.31 | 3.57 | 110.0 → 101.8 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.53 | 2.52 | 13.60 | 99% | 0.42 | 2.23 | 3.10 | 110.1 → 101.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 2.20 | 11.35 | 100% | 0.41 | 1.94 | 2.40 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.163 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.44 | 2.66 | 8.84 | 98% | 0.39 | 2.24 | 3.53 | 50.6 → 72.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 215 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 2.56 | 14.46 | 99% | 0.43 | 2.21 | 3.35 | 105.7 → 102.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.86 | 3.03 | 17.83 | 98% | 0.58 | 2.57 | 3.62 | 320.5 → 312.3 | 7 | 1.1 / 4 |

