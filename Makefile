
.PHONY:	all

SRCS=main.c
OBJS=$(SRCS:%.c=%.o)

COPT=-std=c99 -Wall -Wextra -Werror -O2

all:	main

clean:
	rm -f main $(OBJS)

main:	$(OBJS)
	gcc -o $@ $^

.c.o:
	gcc -c $(COPT) $<
