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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.88 | 17.12 | 99% | 0.38 | 2.61 | 4.17 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.15 | 14.92 | 99% | 0.67 | 2.70 | 4.28 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.27 | 15.68 | 99% | 0.82 | 2.72 | 4.45 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.22 | 85.29 | 99% | 0.75 | 2.76 | 4.44 | 103.2 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.94 | 13.74 | 99% | 0.54 | 2.49 | 4.00 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.99 | 15.94 | 99% | 0.55 | 2.58 | 4.17 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.99 | 12.43 | 99% | 0.52 | 2.58 | 4.17 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 3.18 | 14.52 | 99% | 0.57 | 2.76 | 4.29 | 105.0 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.97 | 87.48 | 99% | 0.51 | 2.60 | 4.11 | 83.5 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 2.98 | 16.16 | 99% | 0.51 | 2.61 | 4.24 | 108.3 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 3.00 | 16.92 | 99% | 0.55 | 2.58 | 3.73 | 108.5 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.65 | 7.88 | 100% | 0.51 | 2.35 | 3.03 | 46.0 → 42.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.331 |
| ArrangingLayoutable TextView | 0.079 |
| AttachingStyle - | 0.073 |
| ArrangingLayoutable NumberDrawing | 0.026 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 3.03 | 4.76 | 100% | 0.43 | 2.64 | 4.62 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 251 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 3.03 | 12.71 | 99% | 0.54 | 2.65 | 4.21 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 3.42 | 16.70 | 98% | 0.64 | 2.94 | 4.25 | 316.6 → 308.6 | 7 | 1.1 / 4 |

