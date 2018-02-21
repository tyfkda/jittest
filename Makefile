
.PHONY:	all

CC=gcc
CPP=g++
LK=g++

COPT=-std=c99 -Wall -Wextra -Werror -O2 -fno-operator-names
CPPOPT=-std=c++11 -Wall -Wextra -Werror -O2 -fno-operator-names

all:
	# Please specify target

clean:
	rm -f main *.o

.c.o:
	$(CC) -c $(COPT) $<

.cpp.o:
	$(CPP) -c $(CPPOPT) $<

simpleinterp:	simpleinterp.o parser.o utils.o
	$(LK) -o $@ $^

optinterp:	optinterp.o parser.o utils.o
	$(LK) -o $@ $^

optinterp2:	optinterp2.o parser.o utils.o
	$(LK) -o $@ $^

optinterp3:	optinterp3.o optutils.o parser.o utils.o
	$(LK) -o $@ $^

simplejit:	simplejit.o jit_utils.o parser.o utils.o
	$(LK) -o $@ $^

simpleasmjit:	simpleasmjit.o parser.o utils.o
	$(LK) -o $@ $^ -lasmjit

optasmjit:	optasmjit.o optutils.o parser.o utils.o
	$(LK) -o $@ $^ -lasmjit

simplexbyakjit:	simplexbyakjit.o parser.o utils.o
	$(LK) -o $@ $^

optxbyakjit:	optxbyakjit.o optutils.o parser.o utils.o
	$(LK) -o $@ $^

.PHONY: test-mandelbrot test-factor

BF=./optxbyakjit
BF_OPT=--verbose

test-mandelbrot:
	$(BF) $(BF_OPT) bf-programs/mandelbrot.bf

test-factor:
	echo 179424691 | $(BF) $(BF_OPT) bf-programs/factor.bf
