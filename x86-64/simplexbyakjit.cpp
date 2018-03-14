// A simple JIT for BF, using the Xbyak library. No optimizations.
//
// Based on simpleasmjit by Eli Bendersky [http://eli.thegreenplace.net]

#include <fstream>
#include <iomanip>
#include <stack>

#define XBYAK_NO_OP_NAMES
#include "xbyak/xbyak.h"

#include "parser.h"
#include "utils.h"

constexpr int MEMORY_SIZE = 30000;

// This function will be invoked from JITed code; not using putchar directly
// since it can be a macro on some systems, so taking its address is
// problematic.
void myputchar(uint8_t c) {
  putchar(c);
}

// ... wrapper for the same reason as myputchar.
uint8_t mygetchar() {
  return getchar();
}

struct BracketLabels {
  BracketLabels(const Xbyak::Label& ol, const Xbyak::Label& cl)
      : open_label(ol), close_label(cl) {}

  Xbyak::Label open_label;
  Xbyak::Label close_label;
};

class SimpleXbyakJit : public Xbyak::CodeGenerator {
public:
  SimpleXbyakJit() : CodeGenerator(100000) {}

  void run(const Program& p, bool verbose) {
    using namespace Xbyak;

    // Compile

    std::stack<BracketLabels> open_bracket_stack;

    const Reg64& dataptr(r13);
    mov(dataptr, rdi);

    for (size_t pc = 0; pc < p.instructions.size(); ++pc) {
      char instruction = p.instructions[pc];
      switch (instruction) {
      case '>':
        // inc %r13
        inc(dataptr);
        break;
      case '<':
        // dec %r13
        dec(dataptr);
        break;
      case '+':
        // addb $1, 0(%r13)
        add(byte[dataptr], 1);
        break;
      case '-':
        // subb $1, 0(%r13)
        sub(byte[dataptr], 1);
        break;
      case '.':
        // call myputchar [dataptr]
        movzx(rdi, byte[dataptr]);
        call(myputchar);
        break;
      case ',':
        // [dataptr] = call mygetchar
        // Store only the low byte to memory to avoid overwriting unrelated data.
        call(mygetchar);
        mov(byte[dataptr], al);
        break;
      case '[': {
        cmp(byte[dataptr], 0);
        Label open_label;
        Label close_label;

        // Jump past the closing ']' if [dataptr] = 0; close_label wasn't bound
        // yet (it will be bound when we handle the matching ']'), but asmjit lets
        // us emit the jump now and will handle the back-patching later.
        jz(close_label, T_NEAR);

        // open_label is bound past the jump; all in all, we're emitting:
        //
        //    cmpb 0(%r13), 0
        //    jz close_label
        // open_label:
        //    ...
        L(open_label);

        // Save both labels on the stack.
        open_bracket_stack.push(BracketLabels(open_label, close_label));
        break;
      }
      case ']': {
        if (open_bracket_stack.empty()) {
          DIE << "unmatched closing ']' at pc=" << pc;
        }
        BracketLabels labels = open_bracket_stack.top();
        open_bracket_stack.pop();

        //    cmpb 0(%r13), 0
        //    jnz open_label
        // close_label:
        //    ...
        cmp(byte[dataptr], 0);
        jnz(labels.open_label, T_NEAR);
        L(labels.close_label);
        break;
      }
      default: { DIE << "bad char '" << instruction << "' at pc=" << pc; }
      }
    }

    ret();

    // Run

    std::vector<uint8_t> memory(MEMORY_SIZE, 0);

    auto func = get();

    func((uint64_t)(memory.data()));

    if (verbose) {
      const char* filename = "/tmp/bjout.bin";
      FILE* outfile = fopen(filename, "wb");
      if (outfile) {
        size_t n = getSize();
        if (fwrite(static_cast<const void*>(getCode()), 1, n, outfile) == n) {
          std::cout << "* emitted code to " << filename << "\n";
        }
        fclose(outfile);
      }

      std::cout << "* Memory nonzero locations:\n";
      for (size_t i = 0, pcount = 0; i < memory.size(); ++i) {
        if (memory[i]) {
          std::cout << std::right << "[" << std::setw(3) << i
                    << "] = " << std::setw(3) << std::left
                    << static_cast<int32_t>(memory[i]) << "      ";
          pcount++;

          if (pcount > 0 && pcount % 4 == 0) {
            std::cout << "\n";
          }
        }
      }
      std::cout << "\n";
    }
  }

private:
  void (*get() const)(uint64_t) { return getCode<void(*)(uint64_t)>(); }
};

int main(int argc, const char** argv) {
  bool verbose = false;
  std::string bf_file_path;
  parse_command_line(argc, argv, &bf_file_path, &verbose);

  Timer t1;
  std::ifstream file(bf_file_path);
  if (!file) {
    DIE << "unable to open file " << bf_file_path;
  }
  Program program = parse_from_stream(file);

  if (verbose) {
    std::cout << "Parsing took: " << t1.elapsed() << "s\n";
    std::cout << "Length of program: " << program.instructions.size() << "\n";
    std::cout << "Program:\n" << program.instructions << "\n";
  }

  if (verbose) {
    std::cout << "[>] Running simplexbyakjit:\n";
  }

  Timer t2;
  SimpleXbyakJit j;
  j.run(program, verbose);

  if (verbose) {
    std::cout << "[<] Done (elapsed: " << t2.elapsed() << "s)\n";
  }

  return 0;
}
