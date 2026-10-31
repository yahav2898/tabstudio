# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 479 ms | 2.6 ms | 23.4 ms |
| Memory pressure | 13 ms | 0.0 ms | 3.0 ms |
