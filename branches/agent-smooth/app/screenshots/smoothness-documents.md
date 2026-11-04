# Twenty long tabs

- The sample with 20 more C# files of about 1,500 lines each, in the IDE's window at 1280 × 800.
- The tab in view colored by Roslyn 4.5 s after the first was opened.

| Action | Cost |
| --- | --- |
| Opening a tab, to its first frame | median 172.5 ms, worst 707.9 ms, 122 KB each |
| Switching to another tab, to its first frame | median 12.7 ms, worst 38.5 ms, 353 KB each |
| A keystroke with 21 tabs open | median 9.6 ms, worst 46.6 ms, 311 KB each |
| Closing a tab | median 14.0 ms, worst 38.5 ms, 447 KB each |

After closing them: 2 of 21 documents and 2 of 21 editors still in memory.

The UI thread: 16 over 50 ms (0 over 250 ms, 0 over 1 s), 1.1 s in all, longest 107 ms during opening the tabs, 0.5 s of it garbage collection; answer p50 2.1 ms, p95 34.2 ms, p99 72.3 ms over 590 questions in 14 s.

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
| 107 ms | 35 ms | 0.4 s | opening the tabs |
| 85 ms | 37 ms | 0.1 s | opening the tabs |
| 85 ms | 15 ms | 0.2 s | opening the tabs |
| 84 ms | 38 ms | 2.8 s | opening the tabs |
| 73 ms | 21 ms | 1.1 s | opening the tabs |
| 72 ms | 15 ms | 0.3 s | opening the tabs |
| 70 ms | 51 ms | 3.1 s | opening the tabs |
| 68 ms | 40 ms | 2.9 s | opening the tabs |
| 64 ms | 33 ms | 2.4 s | opening the tabs |
| 63 ms | 13 ms | 0.4 s | opening the tabs |
| 63 ms | 31 ms | 3.3 s | opening the tabs |
| 62 ms | 36 ms | 4.3 s | opening the tabs |
