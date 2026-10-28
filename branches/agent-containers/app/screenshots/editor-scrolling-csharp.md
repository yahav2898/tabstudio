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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.24 | 9.99 | 99% | 0.52 | 2.76 | 3.58 | 122.9 → 124.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.38 | 13.20 | 99% | 0.66 | 2.75 | 3.60 | 160.8 → 158.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 3.45 | 16.16 | 99% | 0.71 | 2.78 | 3.62 | 196.2 → 180.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.47 | 12.39 | 99% | 0.66 | 2.81 | 3.71 | 140.5 → 138.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.95 | 4.86 | 12.38 | 99% | 2.47 | 2.76 | 3.61 | 117.9 → 111.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 3.29 | 12.43 | 99% | 0.63 | 2.73 | 3.69 | 142.9 → 134.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.04 | 3.39 | 13.17 | 99% | 0.66 | 2.78 | 3.71 | 142.9 → 134.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 3.40 | 13.35 | 99% | 0.65 | 2.81 | 3.69 | 142.4 → 134.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 3.33 | 12.95 | 99% | 0.58 | 2.77 | 3.59 | 120.0 → 115.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 5.39 | 15.98 | 99% | 0.83 | 4.71 | 4.08 | 146.6 → 139.0 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.14 | 3.53 | 13.68 | 99% | 0.68 | 2.90 | 3.53 | 145.9 → 137.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 3.00 | 11.31 | 99% | 0.59 | 2.47 | 2.78 | 81.8 → 78.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.307 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.83 | 2.88 | 2.98 | 100% | 0.49 | 2.47 | 3.45 | 87.8 → 108.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 46 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.11 | 12.53 | 99% | 0.56 | 2.61 | 3.44 | 141.4 → 138.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.50 | 5.81 | 18.41 | 98% | 2.45 | 5.02 | 4.01 | 355.5 → 347.7 | 7 | 1.1 / 4 |

