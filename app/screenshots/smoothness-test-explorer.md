# Test Explorer: 5,000 results arriving one by one

- 10 namespaces, 10 classes in each, 50 tests in each class; Test Explorer shown in a 1280 × 800 window.
- Discovery (the build's 5,000 tests arrive): 39.7 ms, 15672 KB, 1 GCs.

| Run All, the runner reporting from its threads | From the first start to the last result shown | The UI thread meanwhile |
| --- | --- | --- |
| Each start and result posted on its own, as before 0.21.7 | 2.04 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 2.2 ms, p99 7.7 ms over 366 questions in 2 s |
| Queued and shown in batches | 0.06 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 2.2 ms, p95 7.6 ms, p99 7.6 ms over 7 questions in 0 s |
