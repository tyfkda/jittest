// A simple (unoptimized) direct threaded interpreter for BF.
//
// Based on simpleasmjit by Eli Bendersky [http://eli.thegreenplace.net]
#include <cstdint>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <memory>
#include <sstream>
#include <vector>

#include "parser.h"
#include "utils.h"

constexpr int MEMORY_SIZE = 30000;

void simpledt(const Program& p, bool verbose) {
  // Convert instructions to direct thread.
  size_t originalSize = p.instructions.size();
  std::vector<void*> instructions(originalSize + 1);
  for (size_t pc = 0; pc < originalSize; ++pc) {
    void* adr = nullptr;
    char instruction = p.instructions[pc];
    switch (instruction) {
    case '>': adr = &&INC_PTR; break;
    case '<': adr = &&DEC_PTR; break;
    case '+': adr = &&INC_DATA; break;
    case '-': adr = &&DEC_DATA; break;
    case '.': adr = &&READ_STDIN; break;
    case ',': adr = &&WRITE_STDOUT; break;
    case '[': adr = &&JUMP_IF_DATA_ZERO; break;
    case ']': adr = &&JUMP_IF_DATA_NOT_ZERO; break;
    default: { DIE << "bad char '" << instruction << "' at pc=" << pc; }
    }
    instructions[pc] = adr;
  }
  instructions[originalSize] = &&HALT;

  // Initialize state.
  std::vector<uint8_t> memory(MEMORY_SIZE, 0);
  void** pc = &instructions[0];
  size_t dataptr = 0;

#define JUMP_TO_NEXT  goto **(pc++)

  JUMP_TO_NEXT;

  {
  INC_PTR:
    dataptr++;
    JUMP_TO_NEXT;
  DEC_PTR:
    dataptr--;
    JUMP_TO_NEXT;
  INC_DATA:
    memory[dataptr]++;
    JUMP_TO_NEXT;
  DEC_DATA:
    memory[dataptr]--;
    JUMP_TO_NEXT;
  READ_STDIN:
    std::cout.put(memory[dataptr]);
    JUMP_TO_NEXT;
  WRITE_STDOUT:
    memory[dataptr] = std::cin.get();
    JUMP_TO_NEXT;
  JUMP_IF_DATA_ZERO:
    if (memory[dataptr] == 0) {
      int bracket_nesting = 1;
      size_t ipc = pc - 1 - &instructions[0];
      size_t saved_pc = ipc;

      while (bracket_nesting && ++ipc < p.instructions.size()) {
        if (p.instructions[ipc] == ']') {
          bracket_nesting--;
        } else if (p.instructions[ipc] == '[') {
          bracket_nesting++;
        }
      }

      if (!bracket_nesting) {
        pc = &instructions[ipc + 1];
      } else {
        DIE << "unmatched '[' at pc=" << saved_pc;
      }
    }
    JUMP_TO_NEXT;
  JUMP_IF_DATA_NOT_ZERO:
    if (memory[dataptr] != 0) {
      int bracket_nesting = 1;
      size_t ipc = pc - 1 - &instructions[0];
      size_t saved_pc = ipc;

      while (bracket_nesting && ipc > 0) {
        ipc--;
        if (p.instructions[ipc] == '[') {
          bracket_nesting--;
        } else if (p.instructions[ipc] == ']') {
          bracket_nesting++;
        }
      }

      if (!bracket_nesting) {
        pc = &instructions[ipc + 1];
      } else {
        DIE << "unmatched ']' at pc=" << saved_pc;
      }
    }
    JUMP_TO_NEXT;
  }
  HALT:

  // Done running the program. Dump state if verbose.
  if (verbose) {
    std::cout << "* pc=" << pc << "\n";
    std::cout << "* dataptr=" << dataptr << "\n";
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
    std::cout << "[>] Running simpledt:\n";
  }

  Timer t2;
  simpledt(program, verbose);

  if (verbose) {
    std::cout << "[<] Done (elapsed: " << t2.elapsed() << "s)\n";
  }

  return 0;
}
