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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 1.95 | 2.31 | 100% | 0.25 | 1.78 | 2.48 | 85.5 → 86.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.46 | 2.46 | 13.81 | 99% | 0.49 | 1.93 | 2.70 | 123.2 → 123.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 4.65 | 98.94 | 99% | 2.45 | 3.93 | 4.12 | 159.7 → 144.5 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.40 | 2.28 | 12.46 | 99% | 0.41 | 1.93 | 2.67 | 103.2 → 100.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 2.02 | 10.70 | 99% | 0.39 | 1.79 | 2.37 | 81.0 → 74.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.36 | 2.15 | 14.82 | 99% | 0.38 | 1.86 | 2.55 | 106.0 → 97.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.38 | 2.14 | 13.33 | 99% | 0.39 | 1.88 | 2.68 | 105.8 → 97.5 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.36 | 2.11 | 13.66 | 99% | 0.37 | 1.83 | 2.62 | 105.2 → 96.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.33 | 1.99 | 8.02 | 100% | 0.28 | 1.77 | 2.34 | 83.1 → 77.7 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.42 | 2.49 | 11.94 | 99% | 0.55 | 1.95 | 2.83 | 108.4 → 100.3 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.52 | 3.55 | 16.37 | 99% | 1.26 | 2.31 | 2.64 | 108.5 → 100.1 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.82 | 10.72 | 100% | 0.40 | 1.66 | 1.74 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.165 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.33 | 2.00 | 2.24 | 100% | 0.28 | 1.76 | 2.55 | 49.6 → 73.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 12 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 2.14 | 18.48 | 99% | 0.35 | 1.89 | 2.56 | 104.1 → 100.7 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.66 | 2.84 | 24.54 | 98% | 0.50 | 2.44 | 2.85 | 310.9 → 303.0 | 8 | 1.1 / 4 |

