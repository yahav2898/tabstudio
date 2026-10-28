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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.29 | 1.93 | 9.09 | 100% | 0.21 | 1.74 | 2.28 | 86.0 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.38 | 2.00 | 11.61 | 99% | 0.35 | 1.76 | 2.35 | 123.4 → 121.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 2.05 | 12.37 | 99% | 0.45 | 1.76 | 2.37 | 158.5 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.33 | 1.94 | 11.63 | 100% | 0.30 | 1.74 | 2.28 | 103.3 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.27 | 1.89 | 9.45 | 99% | 0.29 | 1.68 | 2.14 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.34 | 2.10 | 13.18 | 100% | 0.40 | 1.79 | 2.42 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.47 | 2.70 | 70.39 | 99% | 1.10 | 2.21 | 2.99 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.31 | 1.93 | 11.60 | 99% | 0.30 | 1.70 | 2.22 | 105.0 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.31 | 1.95 | 8.01 | 100% | 0.27 | 1.74 | 2.31 | 83.4 → 78.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.33 | 2.00 | 11.45 | 99% | 0.32 | 1.77 | 2.31 | 108.4 → 100.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.32 | 1.97 | 12.12 | 99% | 0.29 | 1.76 | 1.96 | 108.4 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.18 | 1.75 | 8.85 | 100% | 0.44 | 1.60 | 1.56 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.109 |
| ArrangingLayoutable TextView | 0.039 |
| ArrangingLayoutable ScrollingTextLayer | 0.034 |
| AttachingStyle - | 0.007 |
| FindingResource - | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable RepeatButton | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.26 | 1.85 | 2.00 | 100% | 0.21 | 1.68 | 2.34 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 37 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.33 | 1.99 | 9.86 | 99% | 0.30 | 1.77 | 2.34 | 104.2 → 100.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.57 | 2.26 | 13.47 | 98% | 0.39 | 1.97 | 2.48 | 316.6 → 308.6 | 7 | 1.1 / 4 |

