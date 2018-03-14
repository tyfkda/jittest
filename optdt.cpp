// A more optimized direct threaded interpreter for BF.
//
// Based on simpleasmjit by Eli Bendersky [http://eli.thegreenplace.net]
#include <algorithm>
#include <fstream>
#include <iostream>
#include <stack>

#include "optutils.h"
#include "parser.h"
#include "utils.h"

using namespace optutils;

constexpr int MEMORY_SIZE = 30000;

struct BfInst {
  BfInst(const void* adr_ = nullptr, int64_t argument_ = 0)
    : adr(adr_), argument(argument_) {}

  const void* adr;
  int64_t argument;
};

void optdt(const Program& p, bool verbose) {
  // Initialize state.
  std::vector<uint8_t> memory(MEMORY_SIZE, 0);
  size_t dataptr = 0;

  Timer t1;
  const std::vector<BfOp> ops = translate_program(p);

  if (verbose) {
    std::cout << "* translation [elapsed " << t1.elapsed() << "s]:\n";

    for (size_t i = 0; i < ops.size(); ++i) {
      std::cout << " [" << i << "] " << BfOpKind_name(ops[i].kind) << " "
                << ops[i].argument << "\n";
    }
  }

  // Convert instructions to direct thread.
  size_t originalSize = ops.size();
  std::vector<BfInst> instructions(originalSize + 1);
  static const void* kLabelAdrs[] = {
    &&INVALID_OP,
    &&INC_PTR,
    &&DEC_PTR,
    &&INC_DATA,
    &&DEC_DATA,
    &&READ_STDIN,
    &&WRITE_STDOUT,
    &&LOOP_SET_TO_ZERO,
    &&LOOP_MOVE_PTR,
    &&LOOP_MOVE_DATA,
    &&JUMP_IF_DATA_ZERO,
    &&JUMP_IF_DATA_NOT_ZERO,
  };
  for (size_t pc = 0; pc < originalSize; ++pc) {
    BfOpKind kind = ops[pc].kind;
    instructions[pc] = BfInst(kLabelAdrs[static_cast<int>(kind)], ops[pc].argument);
  }
  instructions[originalSize] = BfInst(&&HALT, 0);

  // Execute the translated ops in a for loop; pc always gets incremented by the
  // end of each iteration, though some ops may also move it in a less orderly
  // way.
  BfInst* pc = &instructions[0];

#define JUMP_TO_NEXT  goto *((void*)((++pc)->adr))

  --pc;
  JUMP_TO_NEXT;

  {
    {
    INC_PTR:
      dataptr += pc->argument;
      JUMP_TO_NEXT;
    DEC_PTR:
      dataptr -= pc->argument;
      JUMP_TO_NEXT;
    INC_DATA:
      memory[dataptr] += pc->argument;
      JUMP_TO_NEXT;
    DEC_DATA:
      memory[dataptr] -= pc->argument;
      JUMP_TO_NEXT;
    READ_STDIN:
      for (int i = 0; i < pc->argument; ++i) {
        memory[dataptr] = std::cin.get();
      }
      JUMP_TO_NEXT;
    WRITE_STDOUT:
      for (int i = 0; i < pc->argument; ++i) {
        std::cout.put(memory[dataptr]);
      }
      JUMP_TO_NEXT;
    LOOP_SET_TO_ZERO:
      memory[dataptr] = 0;
      JUMP_TO_NEXT;
    LOOP_MOVE_PTR:
      while (memory[dataptr]) {
        dataptr += pc->argument;
      }
      JUMP_TO_NEXT;
    LOOP_MOVE_DATA: {
      if (memory[dataptr]) {
        int64_t move_to_ptr = static_cast<int64_t>(dataptr) + pc->argument;
        memory[move_to_ptr] += memory[dataptr];
        memory[dataptr] = 0;
      }
      JUMP_TO_NEXT;
    }
    JUMP_IF_DATA_ZERO:
      if (memory[dataptr] == 0) {
        pc = &instructions[pc->argument];
      }
      JUMP_TO_NEXT;
    JUMP_IF_DATA_NOT_ZERO:
      if (memory[dataptr] != 0) {
        pc = &instructions[pc->argument];
      }
      JUMP_TO_NEXT;
    INVALID_OP:
      DIE << "INVALID_OP encountered on pc=" << pc;
      JUMP_TO_NEXT;
    }
  }

 HALT:;
}

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
    std::cout << "[>] Running optdt:\n";
  }

  Timer t2;
  optdt(program, verbose);

  if (verbose) {
    std::cout << "[<] Done (elapsed: " << t2.elapsed() << "s)\n";
  }

  return 0;
}
