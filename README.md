# dualSocks
CLI socks5 proxy chainer in C#
This is a work in progress. Seems to be reasonably stable & very fast!

# Usage
dsocks2 [user:pass@]proxy.com[:1080] [proxy2 proxy3 ...]

Listens on all interfaces on port 1080 by default

# Todo
* Create a benchmark for speed tests (have a few ideas to speed it up even more)
* Add direct IPv4/IPv6 connection modes (only "domain connections" supported atm)
* Add basic command line options for configuration
