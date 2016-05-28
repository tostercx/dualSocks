# dualSocks
CLI socks5 proxy chainer in C#

This is a work in progress. Seems to be reasonably stable & very fast!

# Usage
dsocks2 [user:pass@]proxy.com[:1080] [proxy2 proxy3 ...]

Listens on all interfaces on port 1080 by default

# CI Builds
| AppVeyor | Win32 binaries |
|:--------:|:--------------:|
| [![Build status](https://ci.appveyor.com/api/projects/status/dnm5rjry66ydb9u9/branch/master?svg=true)](https://ci.appveyor.com/project/tostercx/dualsocks/branch/master) | [check here](https://ci.appveyor.com/project/tostercx/dualSocks/branch/master/artifacts) |

# Todo
* Add basic command line options for configuration
* Separate library from command line tool
* Add tests to CI
