# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 756 ms | 2.0 ms | 82.7 ms |
| Memory pressure | 19 ms | 0.1 ms | 9.5 ms |
