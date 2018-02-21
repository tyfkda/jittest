// A simple JIT for BF, using the Xbyak library. No optimizations.
//
// Based on optasmjit by Eli Bendersky [http://eli.thegreenplace.net]

#include <fstream>
#include <iomanip>
#include <stack>

#define XBYAK_NO_OP_NAMES
#include "xbyak/xbyak.h"

#include "optutils.h"
#include "parser.h"
#include "utils.h"

using namespace optutils;

constexpr int MEMORY_SIZE = 30000;

namespace {

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

} // namespace

class OptXbyakJit : public Xbyak::CodeGenerator {
public:
  OptXbyakJit() : CodeGenerator(100000) {}

  void run(const Program& p, bool verbose) {
    using namespace Xbyak;

    // Initialize state.
    std::stack<BracketLabels> open_bracket_stack;

    const std::vector<BfOp> ops = translate_program(p);

    if (verbose) {
      std::cout << "==== OPS ====\n";
      for (size_t i = 0; i < ops.size(); ++i) {
        std::cout << std::setw(4) << std::left << i << " ";
        std::cout << BfOpKind_name(ops[i].kind) << " " << ops[i].argument << "\n";
      }
      std::cout << "=============\n";
    }

    // Initialize asmjit's JIT runtime, code holder and assembler.

    // Registers used in the program:
    //
    // r13: the data pointer
    // r14 and rax: used temporarily for some instructions
    // rdi: parameter from the host -- the host passes the address of memory
    // here.

    const Reg64& dataptr(r13);

    // We pass the data pointer as an argument to the JITed function, so it's
    // expected to be in rdi. Move it to r13.
    mov(dataptr, rdi);

    for (size_t pc = 0; pc < ops.size(); ++pc) {
      BfOp op = ops[pc];
      switch (op.kind) {
      case BfOpKind::INC_PTR:
        add(dataptr, op.argument);
        break;
      case BfOpKind::DEC_PTR:
        sub(dataptr, op.argument);
        break;
      case BfOpKind::INC_DATA:
        add(byte[dataptr], op.argument);
        break;
      case BfOpKind::DEC_DATA:
        sub(byte[dataptr], op.argument);
        break;
      case BfOpKind::WRITE_STDOUT:
        for (int i = 0; i < op.argument; ++i) {
          // call myputchar [dataptr]
          movzx(rdi, byte[dataptr]);
          call(myputchar);
        }
        break;
      case BfOpKind::READ_STDIN:
        for (int i = 0; i < op.argument; ++i) {
          // [dataptr] = call mygetchar
          // Store only the low byte to memory to avoid overwriting unrelated
          // data.
          call(mygetchar);
          mov(byte[dataptr], al);
        }
        break;
      case BfOpKind::LOOP_SET_TO_ZERO:
        mov(byte[dataptr], 0);
        break;
      case BfOpKind::LOOP_MOVE_PTR: {
        // Emit a loop that moves the pointer in jumps of op.argument; it's
        // important to do an equivalent of while(...) rather than do...while(...)
        // here so that we don't do the first pointer change if already pointing
        // to a zero.
        //
        // loop:
        //   cmpb 0(%r13), 0
        //   jz endloop
        //   %r13 += argument
        //   jmp loop
        // endloop:
        inLocalLabel();
        L(".loop");
        cmp(byte[dataptr], 0);
        jz(".endloop");
        if (op.argument < 0) {
          sub(dataptr, -op.argument);
        } else {
          add(dataptr, op.argument);
        }
        jmp(".loop");
        L(".endloop");
        outLocalLabel();
        break;
      }
      case BfOpKind::LOOP_MOVE_DATA: {
        // Only move if the current data isn't 0:
        //
        //   cmpb 0(%r13), 0
        //   jz skip_move
        //   <...> move data
        // skip_move:
        inLocalLabel();
        cmp(byte[dataptr], 0);
        jz(".skip_move");

        mov(r14, dataptr);
        if (op.argument < 0) {
          sub(r14, -op.argument);
        } else {
          add(r14, op.argument);
        }
        // Use rax as a temporary holding the value of at the original pointer;
        // then use al to add it to the new location, so that only the target
        // location is affected: addb %al, 0(%r13)
        movzx(rax, byte[dataptr]);
        add(byte[r14], al);
        mov(byte[dataptr], 0);
        L(".skip_move");
        outLocalLabel();
        break;
      }
      case BfOpKind::JUMP_IF_DATA_ZERO: {
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
      case BfOpKind::JUMP_IF_DATA_NOT_ZERO: {
        // These ops have to be properly nested!
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
      case BfOpKind::INVALID_OP:
        DIE << "INVALID_OP encountered on pc=" << pc;
        break;
      }
    }

    ret();

    // Run

    std::vector<uint8_t> memory(MEMORY_SIZE, 0);

    auto func = get();

    Timer texec;

    // Call it, passing the address of memory as a parameter.
    func((uint64_t)memory.data());

    if (verbose) {
      std::cout << "[-] Execution took: " << texec.elapsed() << "s)\n";
    }

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
    std::cout << "[>] Running optasmjit:\n";
  }

  Timer t2;
  OptXbyakJit j;
  j.run(program, verbose);

  if (verbose) {
    std::cout << "[<] Done (elapsed: " << t2.elapsed() << "s)\n";
  }

  return 0;
}
