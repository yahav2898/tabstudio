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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 3.44 | 14.57 | 99% | 0.49 | 3.00 | 5.02 | 123.3 → 125.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.18 | 4.01 | 14.93 | 99% | 0.75 | 3.29 | 5.48 | 162.6 → 160.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 5.41 | 81.62 | 98% | 1.11 | 3.41 | 5.51 | 198.7 → 182.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 3.67 | 17.25 | 99% | 0.64 | 3.10 | 5.17 | 142.3 → 139.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.89 | 3.67 | 15.09 | 99% | 0.66 | 3.07 | 5.16 | 118.5 → 112.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.21 | 5.18 | 14.48 | 99% | 0.85 | 3.46 | 5.84 | 144.9 → 137.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.71 | 13.22 | 99% | 0.68 | 3.16 | 5.32 | 145.0 → 136.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.40 | 14.36 | 99% | 0.59 | 2.92 | 4.75 | 142.1 → 135.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 3.46 | 12.86 | 99% | 0.54 | 3.01 | 4.89 | 120.4 → 115.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 3.93 | 14.74 | 99% | 0.81 | 3.16 | 5.29 | 146.9 → 139.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.91 | 3.62 | 13.92 | 99% | 0.61 | 3.06 | 4.43 | 145.7 → 138.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.13 | 10.07 | 100% | 0.53 | 2.72 | 3.73 | 81.4 → 78.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.221 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 3.41 | 3.63 | 100% | 0.47 | 2.89 | 5.18 | 88.8 → 110.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 187 frames at 120 Hz; the text stood still on 0 of them, 221 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.81 | 14.39 | 99% | 0.66 | 3.17 | 5.30 | 142.6 → 140.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 4.07 | 18.45 | 98% | 0.75 | 3.41 | 5.03 | 355.4 → 348.8 | 8 | 1.1 / 4 |

