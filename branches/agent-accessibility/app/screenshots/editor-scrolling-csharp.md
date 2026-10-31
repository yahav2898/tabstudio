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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.71 | 18.02 | 100% | 0.49 | 2.76 | 4.42 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 3.08 | 12.67 | 100% | 0.56 | 2.63 | 4.21 | 122.1 → 122.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 3.21 | 18.77 | 99% | 0.89 | 2.63 | 4.30 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.93 | 16.89 | 99% | 0.53 | 2.56 | 4.18 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 2.75 | 13.56 | 100% | 0.55 | 2.38 | 3.90 | 80.4 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.68 | 3.41 | 32.72 | 99% | 0.67 | 2.74 | 4.48 | 105.1 → 96.7 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 3.15 | 27.04 | 100% | 0.69 | 2.66 | 4.30 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.94 | 14.96 | 100% | 0.54 | 2.56 | 4.07 | 104.2 → 95.9 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.53 | 2.81 | 14.00 | 99% | 0.43 | 2.48 | 3.95 | 82.7 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.59 | 3.05 | 16.91 | 99% | 0.55 | 2.62 | 4.17 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.60 | 2.88 | 19.78 | 99% | 0.55 | 2.53 | 3.72 | 107.6 → 99.2 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.90 | 10.71 | 100% | 1.52 | 2.46 | 3.31 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.194 |
| AttachingStyle - | 0.067 |
| ArrangingLayoutable ScrollingTextLayer | 0.055 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 2.99 | 38.85 | 98% | 0.38 | 2.47 | 4.15 | 49.2 → 73.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 255 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.60 | 2.97 | 15.30 | 99% | 0.53 | 2.59 | 4.18 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.34 | 5.68 | 23.37 | 99% | 2.45 | 5.17 | 4.73 | 308.9 → 301.2 | 8 | 1.1 / 4 |

