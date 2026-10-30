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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.31 | 2.02 | 12.80 | 99% | 0.21 | 1.86 | 2.41 | 86.1 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 2.18 | 12.51 | 99% | 0.38 | 1.90 | 2.51 | 123.4 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.58 | 2.55 | 13.90 | 99% | 0.64 | 2.07 | 2.89 | 158.6 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 2.16 | 12.09 | 99% | 0.33 | 1.91 | 2.49 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.30 | 2.18 | 10.16 | 100% | 0.41 | 1.85 | 2.45 | 80.6 → 73.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.46 | 2.74 | 15.33 | 100% | 1.07 | 2.24 | 2.94 | 105.8 → 97.6 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.34 | 2.12 | 9.97 | 99% | 0.33 | 1.88 | 2.49 | 105.7 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 2.10 | 14.09 | 99% | 0.35 | 1.89 | 2.50 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 2.07 | 10.03 | 100% | 0.28 | 1.86 | 2.38 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.40 | 2.31 | 12.70 | 99% | 0.38 | 1.95 | 2.72 | 108.4 → 100.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.48 | 2.31 | 13.54 | 99% | 0.40 | 1.97 | 2.43 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 2.26 | 20.77 | 100% | 0.99 | 1.81 | 1.95 | 46.1 → 42.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.366 |
| ArrangingLayoutable ScrollingTextLayer | 0.096 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable NumberDrawing | 0.014 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable Grid | 0.011 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 2.48 | 2.61 | 100% | 0.41 | 2.01 | 2.94 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 2.17 | 12.88 | 99% | 0.35 | 1.93 | 2.64 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.61 | 2.52 | 14.30 | 98% | 0.43 | 2.13 | 2.72 | 316.6 → 308.5 | 7 | 1.1 / 4 |

