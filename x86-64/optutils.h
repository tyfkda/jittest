#pragma once

#include <vector>

#include "parser.h"

namespace optutils {

// Translation to a sequence of BfOps is taken verbatim from
// optimized3_interpteter; all comments from there apply.
enum class BfOpKind {
  INVALID_OP = 0,
  INC_PTR,
  DEC_PTR,
  INC_DATA,
  DEC_DATA,
  READ_STDIN,
  WRITE_STDOUT,
  LOOP_SET_TO_ZERO,
  LOOP_MOVE_PTR,
  LOOP_MOVE_DATA,
  JUMP_IF_DATA_ZERO,
  JUMP_IF_DATA_NOT_ZERO
};

const char* BfOpKind_name(BfOpKind kind);

struct BfOp {
  BfOp(BfOpKind kind_param, int64_t argument_param);

  BfOpKind kind;
  int64_t argument;
};

// Optimizes a loop that starts at loop_start (the opening JUMP_IF_DATA_ZERO).
// The loop runs until the end of ops (implicitly there's a back-jump after the
// last op in ops).
//
// If optimization succeeds, returns a sequence of instructions that replace the
// loop; otherwise, returns an empty vector.
std::vector<BfOp> optimize_loop(const std::vector<BfOp>& ops,
                                size_t loop_start);

// Translates the given program into a vector of BfOps that can be used for fast
// interpretation.
std::vector<BfOp> translate_program(const Program& p);

} // namespace optutils
