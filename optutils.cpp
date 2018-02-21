#include "optutils.h"

#include <stack>

#include "utils.h"

namespace optutils {

const char* BfOpKind_name(BfOpKind kind) {
  switch (kind) {
  case BfOpKind::INC_PTR:
    return "INC_PTR";
  case BfOpKind::DEC_PTR:
    return "DEC_PTR";
  case BfOpKind::INC_DATA:
    return "INC_DATA";
  case BfOpKind::DEC_DATA:
    return "DEC_DATA";
  case BfOpKind::READ_STDIN:
    return "READ_STDIN";
  case BfOpKind::WRITE_STDOUT:
    return "WRITE_STDOUT";
  case BfOpKind::LOOP_SET_TO_ZERO:
    return "LOOP_SET_TO_ZERO";
  case BfOpKind::LOOP_MOVE_PTR:
    return "LOOP_MOVE_PTR";
  case BfOpKind::LOOP_MOVE_DATA:
    return "LOOP_MOVE_DATA";
  case BfOpKind::JUMP_IF_DATA_ZERO:
    return "JUMP_IF_DATA_ZERO";
  case BfOpKind::JUMP_IF_DATA_NOT_ZERO:
    return "JUMP_IF_DATA_NOT_ZERO";
  case BfOpKind::INVALID_OP:
    return "INVALID_OP";
  }
  return nullptr;
}

BfOp::BfOp(BfOpKind kind_param, int64_t argument_param)
  : kind(kind_param), argument(argument_param) {}

// Optimizes a loop that starts at loop_start (the opening JUMP_IF_DATA_ZERO).
// The loop runs until the end of ops (implicitly there's a back-jump after the
// last op in ops).
//
// If optimization succeeds, returns a sequence of instructions that replace the
// loop; otherwise, returns an empty vector.
std::vector<BfOp> optimize_loop(const std::vector<BfOp>& ops,
                                size_t loop_start) {
  std::vector<BfOp> new_ops;

  if (ops.size() - loop_start == 2) {
    BfOp repeated_op = ops[loop_start + 1];
    if (repeated_op.kind == BfOpKind::INC_DATA ||
        repeated_op.kind == BfOpKind::DEC_DATA) {
      new_ops.push_back(BfOp(BfOpKind::LOOP_SET_TO_ZERO, 0));
    } else if (repeated_op.kind == BfOpKind::INC_PTR ||
               repeated_op.kind == BfOpKind::DEC_PTR) {
      new_ops.push_back(
          BfOp(BfOpKind::LOOP_MOVE_PTR, repeated_op.kind == BfOpKind::INC_PTR
                                            ? repeated_op.argument
                                            : -repeated_op.argument));
    }
  } else if (ops.size() - loop_start == 5) {
    // Detect patterns: -<+> and ->+<
    if (ops[loop_start + 1].kind == BfOpKind::DEC_DATA &&
        ops[loop_start + 3].kind == BfOpKind::INC_DATA &&
        ops[loop_start + 1].argument == 1 &&
        ops[loop_start + 3].argument == 1) {

      if (ops[loop_start + 2].kind == BfOpKind::INC_PTR &&
          ops[loop_start + 4].kind == BfOpKind::DEC_PTR &&
          ops[loop_start + 2].argument == ops[loop_start + 4].argument) {
        new_ops.push_back(
            BfOp(BfOpKind::LOOP_MOVE_DATA, ops[loop_start + 2].argument));
      } else if (ops[loop_start + 2].kind == BfOpKind::DEC_PTR &&
                 ops[loop_start + 4].kind == BfOpKind::INC_PTR &&
                 ops[loop_start + 2].argument == ops[loop_start + 4].argument) {
        new_ops.push_back(
            BfOp(BfOpKind::LOOP_MOVE_DATA, -ops[loop_start + 2].argument));
      }
    }
  }
  return new_ops;
}

// Translates the given program into a vector of BfOps that can be used for fast
// interpretation.
std::vector<BfOp> translate_program(const Program& p) {
  size_t pc = 0;
  size_t program_size = p.instructions.size();
  std::vector<BfOp> ops;

  // Throughout the translation loop, this stack contains offsets (in the ops
  // vector) of open brackets (JUMP_IF_DATA_ZERO ops) waiting for a closing
  // bracket. Since brackets nest, these naturally form a stack. The
  // JUMP_IF_DATA_ZERO ops get added to ops with their argument set to 0 until a
  // matching closing bracket is encountered, at which point the argument can be
  // back-patched.
  std::stack<size_t> open_bracket_stack;

  while (pc < program_size) {
    char instruction = p.instructions[pc];
    if (instruction == '[') {
      // Place a jump op with a placeholder 0 offset. It will be patched-up to
      // the right offset when the matching ']' is found.
      open_bracket_stack.push(ops.size());
      ops.push_back(BfOp(BfOpKind::JUMP_IF_DATA_ZERO, 0));
      pc++;
    } else if (instruction == ']') {
      if (open_bracket_stack.empty()) {
        DIE << "unmatched closing ']' at pc=" << pc;
      }
      size_t open_bracket_offset = open_bracket_stack.top();
      open_bracket_stack.pop();

      // Try to optimize this loop; if optimize_loop succeeds, it returns a
      // non-empty vector which we can splice into ops in place of the loop.
      // If the returned vector is empty, we proceed as usual.
      std::vector<BfOp> optimized_loop =
          optimize_loop(ops, open_bracket_offset);

      if (optimized_loop.empty()) {
        // Loop wasn't optimized, so proceed emitting the back-jump to ops. We
        // have the offset of the matching '['. We can use it to create a new
        // jump op for the ']' we're handling, as well as patch up the offset of
        // the matching '['.
        ops[open_bracket_offset].argument = ops.size();
        ops.push_back(
            BfOp(BfOpKind::JUMP_IF_DATA_NOT_ZERO, open_bracket_offset));
      } else {
        // Replace this whole loop with optimized_loop.
        ops.erase(ops.begin() + open_bracket_offset, ops.end());
        ops.insert(ops.end(), optimized_loop.begin(), optimized_loop.end());
      }
      pc++;
    } else {
      // Not a jump; all the other ops can be repeated, so find where the repeat
      // ends.
      size_t start = pc++;
      while (pc < program_size && p.instructions[pc] == instruction) {
        pc++;
      }
      // Here pc points to the first new instruction encountered, or to the end
      // of the program.
      size_t num_repeats = pc - start;

      // Figure out which op kind the instruction represents and add it to the
      // ops.
      BfOpKind kind = BfOpKind::INVALID_OP;
      switch (instruction) {
      case '>':
        kind = BfOpKind::INC_PTR;
        break;
      case '<':
        kind = BfOpKind::DEC_PTR;
        break;
      case '+':
        kind = BfOpKind::INC_DATA;
        break;
      case '-':
        kind = BfOpKind::DEC_DATA;
        break;
      case ',':
        kind = BfOpKind::READ_STDIN;
        break;
      case '.':
        kind = BfOpKind::WRITE_STDOUT;
        break;
      default: { DIE << "bad char '" << instruction << "' at pc=" << start; }
      }

      ops.push_back(BfOp(kind, num_repeats));
    }
  }

  return ops;
}

} // namespace optutils
