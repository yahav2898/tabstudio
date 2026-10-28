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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.24 | 12.98 | 99% | 0.49 | 2.82 | 4.71 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 4.05 | 17.74 | 99% | 1.23 | 3.04 | 5.17 | 124.3 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.70 | 15.14 | 99% | 0.91 | 2.88 | 4.72 | 159.9 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.41 | 15.20 | 99% | 0.62 | 2.84 | 4.67 | 103.9 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.23 | 12.67 | 100% | 0.70 | 2.68 | 4.71 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 3.53 | 82.43 | 99% | 0.74 | 2.83 | 5.13 | 106.6 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 3.29 | 15.86 | 99% | 0.68 | 2.73 | 4.69 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.27 | 13.89 | 100% | 0.64 | 2.72 | 4.65 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.79 | 3.36 | 13.81 | 99% | 0.62 | 2.76 | 4.68 | 83.8 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 3.38 | 15.45 | 99% | 0.65 | 2.78 | 4.82 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 3.30 | 13.85 | 99% | 0.60 | 2.75 | 4.06 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.86 | 4.41 | 100% | 0.64 | 2.41 | 3.39 | 46.5 → 42.5 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.265 |
| ArrangingLayoutable TextView | 0.028 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 3.62 | 5.22 | 100% | 0.53 | 3.11 | 5.31 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 197 frames at 120 Hz; the text stood still on 0 of them, 231 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.24 | 12.48 | 100% | 0.59 | 2.70 | 4.63 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.35 | 3.95 | 17.30 | 98% | 0.79 | 3.18 | 4.82 | 318.4 → 310.4 | 8 | 1.1 / 4 |

