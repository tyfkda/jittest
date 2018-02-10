JIT Test
========

Follow the blog posts to implement BF JIT compiler:

  * [Adventures in JIT compilation: Part 1 - an interpreter - Eli Bendersky's website](https://eli.thegreenplace.net/2017/adventures-in-jit-compilation-part-1-an-interpreter.html)
  * [Adventures in JIT compilation: Part 2 - an x64 JIT - Eli Bendersky's website](https://eli.thegreenplace.net/2017/adventures-in-jit-compilation-part-2-an-x64-jit/)

All codes are written by the original author: [code-for-blog/2017/bfjit at master · eliben/code-for-blog](https://github.com/eliben/code-for-blog/tree/master/2017/bfjit)
(with some modifications.)


### Set up

On host machine:
```sh
$ vagrant up  # Wake up a client
$ vagrant ssh  # Log in to the client
```

On client machine:
```sh
$ cd /vagrant  # Move to the working directory
$ make  # Build
$ ./main  # Run
```
