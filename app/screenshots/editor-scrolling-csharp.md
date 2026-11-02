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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.94 | 13.16 | 100% | 0.40 | 2.58 | 6.18 | 84.9 → 85.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.82 | 16.71 | 99% | 1.35 | 2.80 | 6.61 | 122.1 → 122.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 3.32 | 15.06 | 99% | 0.90 | 2.65 | 6.36 | 158.2 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 3.03 | 16.19 | 99% | 0.56 | 2.57 | 6.15 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.91 | 13.00 | 99% | 0.57 | 2.46 | 5.95 | 80.6 → 73.7 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.74 | 3.22 | 14.58 | 99% | 1.26 | 2.67 | 6.53 | 105.1 → 96.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.69 | 3.05 | 12.53 | 99% | 0.58 | 2.59 | 6.16 | 105.0 → 96.7 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.66 | 3.00 | 17.80 | 99% | 0.54 | 2.57 | 6.05 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 2.97 | 13.94 | 99% | 0.46 | 2.58 | 5.99 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.22 | 12.44 | 99% | 0.65 | 2.71 | 6.40 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.65 | 3.05 | 14.23 | 99% | 0.54 | 2.60 | 5.54 | 107.6 → 99.3 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.63 | 12.49 | 100% | 0.69 | 2.33 | 4.94 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.208 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.65 | 3.06 | 3.15 | 100% | 0.43 | 2.59 | 6.32 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 170 frames at 120 Hz; the text stood still on 0 of them, 205 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.14 | 12.26 | 99% | 0.57 | 2.67 | 6.20 | 103.4 → 100.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 3.57 | 17.85 | 98% | 0.68 | 3.01 | 6.20 | 308.9 → 301.1 | 7 | 1.1 / 4 |

