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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.33 | 2.02 | 12.64 | 99% | 0.26 | 1.82 | 2.50 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 2.17 | 13.89 | 100% | 0.37 | 1.90 | 2.53 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.43 | 2.10 | 13.25 | 99% | 0.49 | 1.79 | 2.47 | 156.8 → 141.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.34 | 2.02 | 13.09 | 99% | 0.34 | 1.78 | 2.54 | 101.6 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.37 | 2.53 | 13.61 | 99% | 1.11 | 1.81 | 2.40 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.41 | 2.74 | 10.83 | 99% | 0.46 | 2.10 | 2.86 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.33 | 1.97 | 14.01 | 99% | 0.32 | 1.78 | 2.29 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.32 | 2.02 | 19.24 | 99% | 0.34 | 1.78 | 2.35 | 103.5 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.30 | 1.92 | 10.27 | 99% | 0.27 | 1.73 | 2.19 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.34 | 2.04 | 12.60 | 99% | 0.34 | 1.77 | 2.42 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.31 | 1.93 | 10.62 | 99% | 0.30 | 1.75 | 1.93 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 1.82 | 10.42 | 100% | 0.49 | 1.66 | 1.63 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.254 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.29 | 1.98 | 2.04 | 100% | 0.23 | 1.75 | 2.41 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 10 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.35 | 2.05 | 18.10 | 99% | 0.33 | 1.82 | 2.36 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.56 | 2.31 | 16.60 | 98% | 0.39 | 1.99 | 2.55 | 307.6 → 299.8 | 7 | 1.1 / 4 |

