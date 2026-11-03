# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 942 ms | 4.5 ms | 23.5 ms |
| Memory pressure | 21 ms | 0.1 ms | 10.2 ms |
