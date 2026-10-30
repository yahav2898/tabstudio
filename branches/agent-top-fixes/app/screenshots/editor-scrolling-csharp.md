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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.74 | 86.70 | 99% | 0.46 | 2.15 | 3.00 | 86.4 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 2.54 | 13.17 | 99% | 0.54 | 2.07 | 2.82 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.58 | 13.83 | 99% | 0.64 | 2.12 | 2.91 | 159.9 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.45 | 12.02 | 100% | 0.45 | 2.06 | 3.29 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.30 | 36.84 | 99% | 0.47 | 1.89 | 2.74 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.53 | 2.68 | 14.14 | 99% | 0.52 | 1.98 | 2.83 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 2.73 | 8.28 | 100% | 0.57 | 2.10 | 3.11 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.43 | 2.19 | 13.87 | 99% | 0.41 | 1.85 | 2.93 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.31 | 1.88 | 7.00 | 100% | 0.27 | 1.71 | 2.18 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.98 | 12.05 | 99% | 0.31 | 1.76 | 2.33 | 109.1 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.32 | 1.95 | 10.23 | 100% | 0.30 | 1.75 | 2.00 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 1.75 | 10.40 | 100% | 0.57 | 1.59 | 1.57 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.135 |
| AttachingStyle - | 0.029 |
| ArrangingLayoutable ScrollingTextLayer | 0.014 |
| MeasuringLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.47 | 3.98 | 18.23 | 98% | 0.37 | 2.36 | 2.63 | 50.4 → 72.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 84 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.34 | 2.03 | 11.07 | 100% | 0.32 | 1.77 | 2.53 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.65 | 2.42 | 14.64 | 98% | 0.43 | 2.05 | 2.63 | 318.3 → 310.3 | 7 | 1.1 / 4 |

