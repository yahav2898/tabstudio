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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 2.79 | 26.84 | 99% | 0.35 | 2.49 | 4.03 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.16 | 15.52 | 99% | 0.62 | 2.66 | 4.35 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 3.34 | 16.85 | 99% | 0.92 | 2.79 | 4.63 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.94 | 15.96 | 99% | 1.42 | 2.85 | 4.59 | 103.3 → 100.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.77 | 12.09 | 100% | 0.53 | 2.43 | 3.91 | 80.6 → 73.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.95 | 13.32 | 99% | 0.56 | 2.53 | 4.10 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 2.99 | 15.72 | 99% | 0.54 | 2.63 | 4.16 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.26 | 17.05 | 99% | 0.69 | 2.69 | 4.24 | 105.0 → 96.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.92 | 9.24 | 100% | 0.47 | 2.55 | 4.04 | 83.5 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 2.85 | 14.99 | 99% | 0.53 | 2.50 | 4.09 | 108.3 → 100.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 2.83 | 12.43 | 100% | 0.51 | 2.52 | 3.58 | 108.5 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.57 | 9.11 | 100% | 0.95 | 2.26 | 3.04 | 46.1 → 42.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| ArrangingLayoutable TextView | 0.062 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable CachedLineNumberMargin | 0.031 |
| ArrangingLayoutable NumberDrawing | 0.026 |
| ArrangingLayoutable LineDrawing | 0.019 |
| RaisingRoutedEvent ScrollBar | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 4.98 | 5.16 | 100% | 2.26 | 2.49 | 4.49 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 261 frames at 120 Hz; the text stood still on 0 of them, 255 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 2.91 | 10.42 | 99% | 0.52 | 2.53 | 4.11 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.04 | 3.40 | 17.47 | 98% | 0.60 | 2.91 | 4.28 | 316.6 → 308.6 | 8 | 1.1 / 4 |

