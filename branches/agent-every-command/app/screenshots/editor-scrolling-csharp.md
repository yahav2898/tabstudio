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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 2.82 | 12.63 | 99% | 0.40 | 2.50 | 4.15 | 85.4 → 86.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.73 | 15.56 | 100% | 1.39 | 3.00 | 5.14 | 123.2 → 123.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.30 | 17.69 | 99% | 0.86 | 2.65 | 4.40 | 159.7 → 144.5 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 3.08 | 15.10 | 99% | 0.58 | 2.62 | 4.26 | 103.1 → 100.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.06 | 12.82 | 99% | 0.61 | 2.58 | 4.27 | 81.0 → 74.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.70 | 3.70 | 16.65 | 99% | 0.77 | 2.80 | 5.05 | 106.1 → 97.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 2.99 | 12.22 | 99% | 0.57 | 2.59 | 4.22 | 105.8 → 97.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 3.02 | 15.04 | 99% | 0.56 | 2.62 | 4.16 | 105.2 → 96.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 3.07 | 14.24 | 99% | 0.56 | 2.68 | 4.26 | 83.1 → 77.7 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.69 | 3.43 | 76.73 | 99% | 0.58 | 2.83 | 4.99 | 108.4 → 100.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.59 | 3.03 | 12.70 | 100% | 0.53 | 2.61 | 3.62 | 108.5 → 100.1 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.65 | 9.89 | 100% | 0.63 | 2.33 | 3.02 | 46.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 3.11 | 3.25 | 100% | 0.42 | 2.59 | 4.56 | 49.4 → 73.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 249 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 3.07 | 20.14 | 99% | 0.55 | 2.61 | 4.23 | 104.1 → 100.7 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 4.39 | 19.37 | 98% | 0.68 | 3.16 | 4.62 | 310.9 → 303.0 | 8 | 1.1 / 4 |

