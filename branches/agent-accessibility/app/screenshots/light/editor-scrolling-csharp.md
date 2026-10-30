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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.40 | 2.14 | 12.56 | 99% | 0.32 | 1.88 | 2.52 | 85.0 → 85.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.50 | 2.36 | 14.27 | 99% | 0.48 | 1.98 | 2.64 | 122.4 → 122.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 3.93 | 14.95 | 99% | 2.40 | 2.24 | 2.98 | 158.3 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 3.06 | 11.93 | 99% | 0.86 | 2.32 | 3.50 | 102.5 → 99.6 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.23 | 14.23 | 99% | 0.49 | 1.86 | 2.68 | 80.3 → 73.4 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.51 | 2.35 | 15.45 | 99% | 0.47 | 1.96 | 2.87 | 105.2 → 96.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.50 | 2.36 | 15.86 | 99% | 0.47 | 2.02 | 2.78 | 105.1 → 96.8 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.49 | 2.43 | 15.19 | 99% | 0.47 | 2.06 | 2.77 | 104.4 → 96.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.45 | 2.35 | 12.82 | 99% | 0.40 | 1.97 | 2.82 | 82.7 → 77.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.40 | 2.15 | 13.27 | 99% | 0.39 | 1.87 | 2.58 | 107.6 → 99.6 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.54 | 2.69 | 17.15 | 99% | 0.75 | 2.10 | 2.50 | 107.7 → 99.5 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.39 | 11.59 | 100% | 1.21 | 1.87 | 2.09 | 46.1 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.221 |
| AttachingStyle - | 0.065 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable LineDrawing | 0.017 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.46 | 2.36 | 2.48 | 100% | 0.37 | 1.93 | 2.84 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 35 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 2.55 | 25.61 | 99% | 0.50 | 2.14 | 2.99 | 103.5 → 100.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.82 | 2.83 | 18.99 | 98% | 0.53 | 2.32 | 3.10 | 309.5 → 301.7 | 7 | 1.1 / 4 |

