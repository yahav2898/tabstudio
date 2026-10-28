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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.27 | 1.13 | 11.54 | 99% | 0.19 | 0.99 | 2.15 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.35 | 1.28 | 10.49 | 99% | 0.33 | 1.02 | 2.21 | 123.3 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.39 | 1.30 | 12.03 | 99% | 0.43 | 1.02 | 2.30 | 158.5 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.31 | 1.27 | 11.05 | 100% | 0.30 | 1.01 | 2.32 | 103.2 → 100.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.30 | 2.38 | 11.26 | 99% | 0.38 | 1.07 | 2.17 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 2.53 | 10.76 | 100% | 0.39 | 1.23 | 2.65 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.30 | 1.21 | 11.23 | 99% | 0.29 | 1.00 | 2.20 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.29 | 1.21 | 11.14 | 99% | 0.28 | 0.98 | 2.14 | 105.0 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.27 | 1.14 | 7.30 | 100% | 0.22 | 0.96 | 2.12 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.29 | 1.21 | 9.83 | 99% | 0.28 | 0.99 | 2.18 | 108.3 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.28 | 1.16 | 8.98 | 100% | 0.26 | 0.99 | 1.90 | 108.4 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.16 | 0.98 | 8.69 | 100% | 0.25 | 0.84 | 1.51 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.129 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| FindingResource - | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.003 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.34 | 2.38 | 7.93 | 100% | 0.31 | 1.07 | 2.45 | 50.0 → 72.1 | 2 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 21 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.29 | 1.16 | 11.51 | 99% | 0.29 | 0.98 | 2.19 | 104.1 → 100.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.50 | 1.42 | 13.27 | 98% | 0.32 | 1.16 | 2.26 | 316.5 → 308.5 | 7 | 1.1 / 4 |

