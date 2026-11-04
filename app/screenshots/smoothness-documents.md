# Twenty long tabs

- The sample with 20 more C# files of about 1,500 lines each, in the IDE's window at 1280 × 800.
- The tab in view colored by Roslyn 5.1 s after the first was opened.

| Action | Cost |
| --- | --- |
| Opening a tab, to its first frame | median 208.2 ms, worst 932.7 ms, 125 KB each |
| Switching to another tab, to its first frame | median 12.0 ms, worst 54.5 ms, 339 KB each |
| A keystroke with 21 tabs open | median 10.6 ms, worst 52.0 ms, 310 KB each |
| Closing a tab | median 12.9 ms, worst 30.5 ms, 451 KB each |

After closing them: 2 of 21 documents and 2 of 21 editors still in memory.

The UI thread: 23 over 50 ms (1 over 250 ms, 1 over 1 s), 3.8 s in all, longest 2147 ms during closing the tabs, 2.9 s of it garbage collection; answer p50 2.1 ms, p95 30.5 ms, p99 84.4 ms over 694 questions in 16 s.

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
| 2147 ms | 2143 ms | 7.7 s | closing the tabs |
| 139 ms | 43 ms | 0.6 s | opening the tabs |
| 121 ms | 15 ms | 0.3 s | opening the tabs |
| 97 ms | 27 ms | 2.9 s | opening the tabs |
| 90 ms | 31 ms | 1.2 s | opening the tabs |
| 86 ms | 45 ms | 5.0 s | opening the tabs |
| 84 ms | 52 ms | 4.8 s | opening the tabs |
| 81 ms | 32 ms | 0.8 s | opening the tabs |
| 78 ms | 47 ms | 3.1 s | opening the tabs |
| 74 ms | 45 ms | 4.6 s | opening the tabs |
| 71 ms | 26 ms | 2.6 s | opening the tabs |
| 67 ms | 36 ms | 2.5 s | opening the tabs |
