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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.38 | 3.46 | 100% | 0.39 | 2.03 | 2.95 | 84.8 → 85.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.51 | 2.41 | 16.76 | 99% | 0.54 | 1.98 | 2.99 | 122.0 → 122.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 3.01 | 18.33 | 99% | 1.33 | 2.34 | 3.21 | 158.1 → 143.0 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.26 | 28.75 | 99% | 1.06 | 2.37 | 3.57 | 102.3 → 99.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.43 | 15.38 | 99% | 0.67 | 2.02 | 3.41 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.57 | 2.51 | 14.73 | 99% | 0.59 | 2.04 | 3.04 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 2.62 | 16.44 | 99% | 0.53 | 2.14 | 3.20 | 104.8 → 96.5 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.61 | 15.74 | 99% | 0.56 | 2.17 | 3.06 | 104.2 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.59 | 2.54 | 13.36 | 99% | 0.46 | 2.14 | 2.89 | 82.6 → 77.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.68 | 2.96 | 15.99 | 99% | 0.60 | 2.24 | 3.28 | 107.4 → 99.3 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.77 | 3.16 | 15.84 | 100% | 1.20 | 2.49 | 2.96 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.45 | 11.57 | 100% | 0.52 | 2.02 | 2.33 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.33 | 2.20 | 2.73 | 100% | 0.42 | 1.90 | 3.12 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 29 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.71 | 21.91 | 99% | 0.57 | 2.21 | 3.32 | 103.2 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.92 | 4.01 | 24.66 | 98% | 0.65 | 2.60 | 3.32 | 308.8 → 300.9 | 8 | 1.1 / 4 |

