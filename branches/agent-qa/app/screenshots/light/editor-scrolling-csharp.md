# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 2.06 | 9.36 | 100% | 0.25 | 1.90 | 2.46 | 86.7 → 87.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 2.33 | 13.11 | 99% | 0.43 | 1.98 | 3.02 | 124.6 → 122.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 2.34 | 22.99 | 99% | 0.51 | 1.99 | 3.05 | 160.2 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 2.15 | 10.82 | 100% | 0.34 | 1.92 | 2.47 | 104.2 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 2.70 | 11.47 | 99% | 1.16 | 2.01 | 2.71 | 81.1 → 73.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.40 | 2.48 | 12.70 | 99% | 0.37 | 2.05 | 2.86 | 106.8 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 2.13 | 11.32 | 99% | 0.34 | 1.92 | 2.49 | 106.7 → 98.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 2.15 | 12.95 | 99% | 0.36 | 1.91 | 2.52 | 106.0 → 97.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 2.09 | 9.87 | 99% | 0.33 | 1.87 | 2.56 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.36 | 2.13 | 12.91 | 99% | 0.34 | 1.89 | 2.61 | 109.3 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.38 | 2.33 | 12.32 | 99% | 0.41 | 1.93 | 2.30 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 2.39 | 9.60 | 100% | 1.20 | 1.87 | 2.08 | 46.1 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.156 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.27 | 2.04 | 7.93 | 100% | 0.28 | 1.77 | 2.47 | 50.4 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 2.13 | 11.25 | 99% | 0.33 | 1.87 | 2.59 | 105.1 → 101.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.61 | 2.42 | 14.63 | 98% | 0.41 | 2.09 | 2.62 | 318.7 → 310.6 | 7 | 1.1 / 4 |

