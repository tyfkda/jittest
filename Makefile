
.PHONY:	all

SRCS=main.c
OBJS=$(SRCS:%.c=%.o)

CC=gcc
CPP=g++
LK=g++

COPT=-std=c99 -Wall -Wextra -Werror -O2
CPPOPT=-std=c++0x -Wall -Wextra -Werror -O2

all:	main

clean:
	rm -f main $(OBJS)

main:	$(OBJS)
	$(LK) -o $@ $^

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

optinterp3:	optinterp3.o parser.o utils.o
	$(LK) -o $@ $^

.PHONY: test-mandelbrot test-factor

BF=./optinterp3
BF_OPT=--verbose

test-mandelbrot:
	$(BF) $(BF_OPT) bf-programs/mandelbrot.bf

test-factor:
	echo 179424691 | $(BF) $(BF_OPT) bf-programs/factor.bf
