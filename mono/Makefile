
.PHONY:	all

all:
	# Please specify target

clean:
	rm -f *.exe

BfInterp.exe:	BfInterp.cs BfUtil.cs
	mcs $^

BfJit.exe:	BfJit.cs BfUtil.cs
	mcs $^

BfOptJit.exe:	BfOptJit.cs BfUtil.cs
	mcs $^

.PHONY: test-mandelbrot test-factor

BF=BfOptJit.exe

test-mandelbrot:
	mono $(BF) ../bf-programs/mandelbrot.bf

test-factor:
	echo 179424691 | mono $(BF) ../bf-programs/factor.bf
