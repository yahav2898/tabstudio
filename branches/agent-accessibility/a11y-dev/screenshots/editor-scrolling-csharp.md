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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 2.28 | 9.28 | 100% | 0.38 | 1.99 | 2.78 | 84.8 → 85.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.54 | 2.39 | 13.89 | 99% | 0.45 | 2.03 | 2.77 | 122.0 → 122.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.53 | 2.44 | 14.53 | 99% | 0.58 | 2.02 | 2.84 | 158.0 → 142.9 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.45 | 2.26 | 14.57 | 99% | 0.41 | 1.96 | 2.69 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.41 | 2.30 | 11.91 | 99% | 0.47 | 1.94 | 2.72 | 80.4 → 73.5 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.42 | 2.73 | 14.18 | 99% | 0.60 | 2.10 | 2.86 | 105.0 → 96.6 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.56 | 2.75 | 78.91 | 99% | 0.96 | 2.18 | 3.07 | 104.8 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.41 | 2.24 | 15.15 | 99% | 0.37 | 1.97 | 2.56 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.38 | 2.25 | 10.58 | 99% | 0.38 | 1.94 | 2.62 | 82.6 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.43 | 2.26 | 15.01 | 99% | 0.42 | 1.94 | 2.69 | 107.4 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.39 | 2.22 | 12.89 | 99% | 0.39 | 1.95 | 2.25 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 1.96 | 8.70 | 100% | 0.73 | 1.76 | 1.78 | 46.3 → 42.6 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.237 |
| ArrangingLayoutable ScrollingTextLayer | 0.049 |
| ArrangingLayoutable Track | 0.015 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.010 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.37 | 2.21 | 2.33 | 100% | 0.33 | 1.88 | 2.78 | 49.3 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 17 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.55 | 2.35 | 22.86 | 99% | 0.48 | 2.01 | 2.97 | 103.2 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.90 | 2.82 | 18.14 | 98% | 0.59 | 2.31 | 3.17 | 308.8 → 300.9 | 6 | 1.1 / 4 |

