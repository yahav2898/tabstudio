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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 3.16 | 22.21 | 100% | 0.58 | 2.62 | 7.82 | 86.6 → 87.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.26 | 3.79 | 22.91 | 100% | 0.97 | 2.74 | 8.03 | 124.3 → 122.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 3.60 | 24.73 | 99% | 1.11 | 2.76 | 8.24 | 160.0 → 144.4 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.52 | 24.84 | 99% | 0.80 | 2.79 | 8.14 | 104.0 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 3.33 | 14.77 | 100% | 0.81 | 2.60 | 8.35 | 81.4 → 74.0 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 1.08 | 3.33 | 24.44 | 99% | 0.80 | 2.71 | 8.01 | 106.6 → 98.2 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 3.42 | 24.77 | 100% | 0.93 | 2.79 | 8.20 | 106.5 → 98.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.13 | 3.43 | 12.36 | 99% | 0.85 | 2.72 | 8.11 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.31 | 20.88 | 100% | 0.66 | 2.66 | 7.96 | 83.9 → 78.4 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 4.19 | 15.23 | 100% | 1.67 | 2.84 | 8.40 | 109.2 → 101.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.12 | 3.41 | 22.77 | 100% | 0.77 | 2.73 | 7.36 | 109.2 → 100.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 3.00 | 21.05 | 100% | 1.19 | 2.36 | 6.54 | 46.6 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.358 |
| AttachingStyle - | 0.038 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.47 | 4.15 | 6.01 | 100% | 0.69 | 3.39 | 11.80 | 50.6 → 72.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2247 ms over 137 frames at 120 Hz; the text stood still on 0 of them, 172 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 3.47 | 24.45 | 99% | 0.78 | 2.76 | 8.05 | 104.9 → 101.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.74 | 5.71 | 29.47 | 99% | 2.51 | 3.26 | 8.30 | 318.5 → 310.4 | 5 | 1.1 / 4 |

