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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 1.56 | 9.86 | 99% | 0.29 | 1.30 | 3.30 | 86.7 → 87.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.93 | 13.89 | 99% | 0.56 | 1.40 | 3.49 | 124.5 → 122.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 4.05 | 87.62 | 99% | 2.50 | 2.45 | 5.44 | 160.2 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.46 | 1.57 | 12.07 | 99% | 0.41 | 1.26 | 3.32 | 104.1 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.37 | 1.47 | 12.07 | 99% | 0.40 | 1.16 | 3.20 | 81.1 → 73.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.42 | 1.55 | 13.13 | 99% | 0.43 | 1.24 | 3.33 | 106.7 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.43 | 1.57 | 13.89 | 99% | 0.42 | 1.25 | 3.33 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.44 | 1.53 | 12.49 | 99% | 0.43 | 1.25 | 3.28 | 106.0 → 97.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.77 | 11.80 | 99% | 0.46 | 1.44 | 3.46 | 83.9 → 78.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 2.66 | 12.60 | 100% | 0.47 | 1.40 | 3.60 | 109.3 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.44 | 1.53 | 13.40 | 99% | 0.38 | 1.24 | 2.90 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.28 | 8.29 | 100% | 0.40 | 1.03 | 2.36 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.148 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.37 | 1.31 | 1.56 | 100% | 0.30 | 1.13 | 3.29 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 2.86 | 13.43 | 99% | 0.57 | 1.52 | 3.64 | 105.0 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.80 | 2.97 | 14.91 | 98% | 0.49 | 1.71 | 3.64 | 318.7 → 310.6 | 7 | 1.1 / 4 |

