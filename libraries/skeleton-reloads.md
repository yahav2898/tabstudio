# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 908 ms | 2.7 ms | 14.4 ms |
| Memory pressure | 24 ms | 0.2 ms | 12.5 ms |
