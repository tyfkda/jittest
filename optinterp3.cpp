// A more optimized interpreter for BF.
//
// Eli Bendersky [http://eli.thegreenplace.net]
// This code is in the public domain.
#include <algorithm>
#include <fstream>
#include <iostream>
#include <stack>

#include "optutils.h"
#include "parser.h"
#include "utils.h"

using namespace optutils;

constexpr int MEMORY_SIZE = 30000;

void optinterp3(const Program& p, bool verbose) {
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

  // Execute the translated ops in a for loop; pc always gets incremented by the
  // end of each iteration, though some ops may also move it in a less orderly
  // way.
  // Note: the pre-computation of ops_size shouldn't be necessary (since ops is
  // const) but it helps gcc 4.8 generate faster code.
  size_t ops_size = ops.size();
  for (size_t pc = 0; pc < ops_size; ++pc) {
    BfOp op = ops[pc];
    switch (op.kind) {
    case BfOpKind::INC_PTR:
      dataptr += op.argument;
      break;
    case BfOpKind::DEC_PTR:
      dataptr -= op.argument;
      break;
    case BfOpKind::INC_DATA:
      memory[dataptr] += op.argument;
      break;
    case BfOpKind::DEC_DATA:
      memory[dataptr] -= op.argument;
      break;
    case BfOpKind::READ_STDIN:
      for (int i = 0; i < op.argument; ++i) {
        memory[dataptr] = std::cin.get();
      }
      break;
    case BfOpKind::WRITE_STDOUT:
      for (int i = 0; i < op.argument; ++i) {
        std::cout.put(memory[dataptr]);
      }
      break;
    case BfOpKind::LOOP_SET_TO_ZERO:
      memory[dataptr] = 0;
      break;
    case BfOpKind::LOOP_MOVE_PTR:
      while (memory[dataptr]) {
        dataptr += op.argument;
      }
      break;
    case BfOpKind::LOOP_MOVE_DATA: {
      if (memory[dataptr]) {
        int64_t move_to_ptr = static_cast<int64_t>(dataptr) + op.argument;
        memory[move_to_ptr] += memory[dataptr];
        memory[dataptr] = 0;
      }
      break;
    }
    case BfOpKind::JUMP_IF_DATA_ZERO:
      if (memory[dataptr] == 0) {
        pc = op.argument;
      }
      break;
    case BfOpKind::JUMP_IF_DATA_NOT_ZERO:
      if (memory[dataptr] != 0) {
        pc = op.argument;
      }
      break;
    case BfOpKind::INVALID_OP:
      DIE << "INVALID_OP encountered on pc=" << pc;
      break;
    }
  }
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
    std::cout << "[>] Running optinterp3:\n";
  }

  Timer t2;
  optinterp3(program, verbose);

  if (verbose) {
    std::cout << "[<] Done (elapsed: " << t2.elapsed() << "s)\n";
  }

  return 0;
}
