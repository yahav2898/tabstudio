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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.83 | 13.04 | 100% | 0.35 | 2.55 | 4.01 | 85.4 → 86.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.20 | 14.00 | 99% | 0.59 | 2.71 | 4.22 | 123.2 → 123.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.42 | 16.61 | 99% | 0.84 | 2.69 | 4.31 | 159.7 → 144.5 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 4.89 | 35.52 | 99% | 1.31 | 2.87 | 4.98 | 103.2 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 2.82 | 13.71 | 99% | 0.54 | 2.41 | 3.89 | 81.0 → 74.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.56 | 2.98 | 16.76 | 99% | 0.54 | 2.58 | 4.06 | 106.0 → 97.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.58 | 3.01 | 24.42 | 99% | 0.55 | 2.62 | 4.16 | 105.8 → 97.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.94 | 15.96 | 100% | 0.55 | 2.56 | 4.07 | 105.2 → 96.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.64 | 4.23 | 94.98 | 99% | 0.58 | 2.75 | 4.25 | 83.2 → 77.8 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.58 | 3.03 | 14.33 | 99% | 0.56 | 2.62 | 4.15 | 108.4 → 100.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.59 | 2.99 | 15.49 | 99% | 0.52 | 2.61 | 3.59 | 108.5 → 100.1 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.61 | 10.56 | 100% | 1.02 | 2.31 | 2.89 | 46.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.72 | 5.25 | 5.41 | 100% | 2.50 | 2.85 | 5.13 | 49.5 → 74.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 245 frames at 120 Hz; the text stood still on 0 of them, 266 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 2.93 | 23.52 | 99% | 0.54 | 2.54 | 4.11 | 104.2 → 100.7 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 3.49 | 18.14 | 98% | 0.65 | 2.92 | 4.21 | 310.9 → 303.0 | 7 | 1.1 / 4 |

