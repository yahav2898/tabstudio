# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.49 | 11.79 | 99% | 0.51 | 2.05 | 4.85 | 116.0 → 117.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 3.37 | 13.85 | 99% | 0.77 | 2.38 | 4.91 | 155.2 → 152.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 2.29 | 13.12 | 99% | 0.72 | 1.77 | 4.19 | 190.5 → 174.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.12 | 11.10 | 99% | 0.51 | 1.72 | 4.22 | 134.1 → 131.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.09 | 9.82 | 99% | 0.51 | 1.71 | 4.34 | 110.6 → 103.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 2.88 | 12.07 | 99% | 0.59 | 2.40 | 4.55 | 136.7 → 128.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 2.00 | 10.20 | 99% | 0.50 | 1.60 | 4.02 | 136.3 → 128.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 1.95 | 10.30 | 99% | 0.50 | 1.62 | 3.91 | 135.6 → 127.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.03 | 12.18 | 99% | 0.45 | 1.65 | 4.11 | 112.9 → 108.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.04 | 8.95 | 100% | 0.65 | 2.10 | 4.94 | 139.9 → 131.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.11 | 10.72 | 99% | 0.55 | 1.74 | 4.02 | 138.9 → 130.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.73 | 10.04 | 100% | 0.41 | 1.36 | 3.10 | 73.8 → 70.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.196 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.97 | 11.65 | 98% | 0.41 | 1.57 | 4.19 | 80.9 → 102.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 199 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.19 | 8.45 | 100% | 0.52 | 1.74 | 4.46 | 134.6 → 131.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.24 | 2.87 | 15.43 | 99% | 0.66 | 2.22 | 4.50 | 348.1 → 341.2 | 7 | 1.1 / 4 |

