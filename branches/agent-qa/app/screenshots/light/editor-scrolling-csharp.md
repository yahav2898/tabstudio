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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.39 | 2.06 | 9.62 | 100% | 0.28 | 1.88 | 2.58 | 86.1 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 2.22 | 12.96 | 99% | 0.44 | 1.88 | 2.61 | 123.4 → 121.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.57 | 2.58 | 14.43 | 99% | 0.59 | 2.09 | 2.91 | 158.5 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.43 | 2.32 | 9.35 | 100% | 0.40 | 1.98 | 2.73 | 103.3 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 2.49 | 14.07 | 99% | 0.49 | 2.02 | 2.72 | 80.6 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.42 | 2.21 | 13.30 | 99% | 0.43 | 1.88 | 2.77 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.42 | 15.51 | 99% | 0.44 | 2.00 | 2.94 | 105.7 → 97.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.42 | 14.53 | 99% | 0.48 | 2.02 | 2.91 | 105.0 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.50 | 9.64 | 100% | 0.49 | 2.06 | 2.97 | 83.4 → 78.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.55 | 13.13 | 99% | 0.56 | 2.08 | 3.16 | 108.3 → 100.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.98 | 15.69 | 100% | 0.64 | 2.35 | 3.03 | 108.5 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.16 | 11.82 | 100% | 1.01 | 1.80 | 2.15 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.232 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
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
| touch drag and flick (real touch input) | 46 | 0.68 | 2.49 | 2.72 | 100% | 0.46 | 2.02 | 3.44 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 20 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 2.50 | 13.95 | 99% | 0.48 | 2.05 | 2.94 | 104.2 → 100.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.97 | 4.82 | 22.22 | 98% | 0.66 | 4.32 | 3.27 | 316.5 → 308.5 | 8 | 1.1 / 4 |

