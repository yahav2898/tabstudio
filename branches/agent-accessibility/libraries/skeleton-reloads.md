# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1485 ms | 2.4 ms | 64.0 ms |
| Memory pressure | 34 ms | 0.1 ms | 15.2 ms |
