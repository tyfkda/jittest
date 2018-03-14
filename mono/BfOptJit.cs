using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

public interface IBfProgram {
  void Invoke(byte[] memory);
}

// optimized3_interpteter; all comments from there apply.
public enum BfOpKind {
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

public class BfOp {
  public BfOpKind kind;
  public int argument;

  public BfOp(BfOpKind kind_param, int argument_param) {
    this.kind = kind_param;
    this.argument = argument_param;
  }
};

class BracketLabels {
  public readonly Label openLabel;
  public readonly Label closeLabel;

  public BracketLabels(Label ol, Label cl) {
    openLabel = ol;
    closeLabel = cl;
  }
};

public class BfGen {
  public IBfProgram Gen(List<BfOp> ops) {
    var className = "CBf";
    AssemblyBuilder theAssembly = EmitAssembly(className, ops);
    //theAssembly.Save("MBf.dll");
    return (IBfProgram)theAssembly.CreateInstance(className, false, BindingFlags.ExactBinding, null,
                                                  new Object[] {}, null, null);
  }

  private AssemblyBuilder EmitAssembly(string className, List<BfOp> ops) {
    AssemblyName assemblyName = new AssemblyName();
    assemblyName.Name = "Bf";

    AssemblyBuilder newAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    //AssemblyBuilder newAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
    ModuleBuilder newModule = newAssembly.DefineDynamicModule("MBf");
    //ModuleBuilder newModule = newAssembly.DefineDynamicModule("MBf", "MBf.dll");

    TypeBuilder myType = newModule.DefineType(className, TypeAttributes.Public);

    myType.AddInterfaceImplementation(typeof(IBfProgram));

    // Define constructor
    DefineConstructor(myType);

    MethodBuilder simpleMethod = DefineInvokeMethod(myType, ops);
    myType.DefineMethodOverride(simpleMethod, typeof(IBfProgram).GetMethod("Invoke"));

    myType.CreateType();

    return newAssembly;
  }

  private void DefineConstructor(TypeBuilder myType) {
    ConstructorBuilder ctor = myType.DefineConstructor(MethodAttributes.Public,
                                                       CallingConventions.Standard,
                                                       new Type[] {});
    ILGenerator generator = ctor.GetILGenerator();
    // Noop
    generator.Emit(OpCodes.Ret);
  }

  private MethodBuilder DefineInvokeMethod(TypeBuilder myType, List<BfOp> ops) {
    MethodBuilder simpleMethod = myType.DefineMethod("Invoke",
                                                     MethodAttributes.Public | MethodAttributes.Virtual,
                                                     typeof(void),  // returnType
                                                     new Type[] {typeof(byte[])});  // ;paramTypes

    ILGenerator generator = simpleMethod.GetILGenerator();

    MethodInfo putcharMI = typeof(BfUtil).GetMethod("PutChar",
                                                    new Type[] {typeof(char)});
    MethodInfo getcharMI = typeof(BfUtil).GetMethod("GetChar",
                                                    new Type[] {});

    Stack<BracketLabels> openBracketStack = new Stack<BracketLabels>();

    LocalBuilder dataptr = generator.DeclareLocal(typeof(int));  // local0: pc
    LocalBuilder tempptr = generator.DeclareLocal(typeof(int));  // local1: temporary

    generator.Emit(OpCodes.Ldc_I4_0);
    generator.Emit(OpCodes.Stloc, dataptr);  // dataptr = 0

    for (int pc = 0; pc < ops.Count; ++pc) {
      BfOp op = ops[pc];
      switch (op.kind) {
      case BfOpKind.INC_PTR:
        generator.Emit(OpCodes.Ldloc, dataptr);
        generator.Emit(OpCodes.Ldc_I4, op.argument);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Stloc, dataptr);  // dataptr += <op.argument>
        break;
      case BfOpKind.DEC_PTR:
        generator.Emit(OpCodes.Ldloc, dataptr);
        generator.Emit(OpCodes.Ldc_I4, op.argument);
        generator.Emit(OpCodes.Sub);
        generator.Emit(OpCodes.Stloc, dataptr);  // dataptr += <op.argument>
        break;
      case BfOpKind.INC_DATA:
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        generator.Emit(OpCodes.Ldc_I4, op.argument);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] += <op.argument>
        break;
      case BfOpKind.DEC_DATA:
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        generator.Emit(OpCodes.Ldc_I4, op.argument);
        generator.Emit(OpCodes.Sub);
        generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] -= <op.argument>
        break;
      case BfOpKind.WRITE_STDOUT:
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        for (int i = 0; i < op.argument; ++i) {
          if (i < op.argument - 1)
            generator.Emit(OpCodes.Dup);
          generator.EmitCall(OpCodes.Call, putcharMI, null);  // putchar(memory[dataptr])
        }
        break;
      case BfOpKind.READ_STDIN:
        for (int i = 0; i < op.argument; ++i) {
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.EmitCall(OpCodes.Call, getcharMI, null);  // getchar()
          generator.Emit(OpCodes.Stelem_I4);  // memory[pc] = getchar()
        }
        break;

      case BfOpKind.LOOP_SET_TO_ZERO:
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldc_I4_0);  // 0
        generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] = 0
        break;
      case BfOpKind.LOOP_MOVE_PTR:
        {
          Label loop = generator.DefineLabel();
          Label endloop = generator.DefineLabel();
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
          generator.MarkLabel(loop);
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldc_I4, 255);
          generator.Emit(OpCodes.And);
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Beq, endloop);  // if memory[dataptr] == 0 goto endloop

          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          if (op.argument < 0) {
            generator.Emit(OpCodes.Ldc_I4, -op.argument);
            generator.Emit(OpCodes.Sub);
          } else {
            generator.Emit(OpCodes.Ldc_I4, op.argument);
            generator.Emit(OpCodes.Add);
          }
          generator.Emit(OpCodes.Stloc, dataptr);  // dataptr += <op.argument>
          generator.Emit(OpCodes.Br, loop);
          generator.MarkLabel(endloop);
        }
        break;
      case BfOpKind.LOOP_MOVE_DATA:
        {
          // Only move if the current data isn't 0:
          //
          //   cmpb 0(%r13), 0
          //   jz skip_move
          //   <...> move data
          // skip_move:
          Label skip_move = generator.DefineLabel();
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldc_I4, 255);
          generator.Emit(OpCodes.And);
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Beq, skip_move);

          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          if (op.argument < 0) {
            generator.Emit(OpCodes.Ldc_I4, -op.argument);
            generator.Emit(OpCodes.Sub);
          } else {
            generator.Emit(OpCodes.Ldc_I4, op.argument);
            generator.Emit(OpCodes.Add);
          }
          generator.Emit(OpCodes.Stloc, tempptr);  // temp = dataptr + <op.argument>

          // Use rax as a temporary holding the value of at the original pointer;
          // then use al to add it to the new location, so that only the target
          // location is affected: addb %al, 0(%r13)
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, tempptr);  // temp
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, tempptr);  // temp
          generator.Emit(OpCodes.Ldelem_I4);  // memory[temp]
          generator.Emit(OpCodes.Add);  // memory[dataptr] + memory[temp]
          generator.Emit(OpCodes.Stelem_I4);  // memory[temp] = memory[dataptr] + memory[temp]

          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] = 0

          generator.MarkLabel(skip_move);
        }
        break;

      case BfOpKind.JUMP_IF_DATA_ZERO:
        {
          Label openLabel = generator.DefineLabel();
          Label closeLabel = generator.DefineLabel();
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldc_I4, 255);
          generator.Emit(OpCodes.And);
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Beq, closeLabel);  // if memory[dataptr] == 0 goto closeLabel
          generator.MarkLabel(openLabel);
          openBracketStack.Push(new BracketLabels(openLabel, closeLabel));
        }
        break;
      case BfOpKind.JUMP_IF_DATA_NOT_ZERO:
        {
          if (openBracketStack.Count == 0) {
            BfUtil.DIE($"Unmatched closing ']' at pc={pc}");
          }
          BracketLabels labels = openBracketStack.Pop();
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldc_I4, 255);
          generator.Emit(OpCodes.And);
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Bne_Un, labels.openLabel);  // if memory[dataptr] != 0 goto openLabel
          generator.MarkLabel(labels.closeLabel);
        }
        break;
      default:
        BfUtil.DIE($"INVALID_OP encountered on pc={pc}");
        break;
      }
    }

    generator.Emit(OpCodes.Ret);

    return simpleMethod;
  }
}

public class BfOptJit {
  public static void Main(string[] args) {
    if (args.Length < 1) {
      BfUtil.DIE("argv < 1");
    }

    string bfCode = BfUtil.LoadProgram(args[0]);
    List<BfOp> optimized = translate_program(bfCode);
    //foreach (BfOp op in optimized) {
    //  Console.WriteLine($"op={op.kind}, argument={op.argument}");
    //}

    var gen = new BfGen();
    var program = gen.Gen(optimized);
    byte[] memory = new byte[30000];
    program.Invoke(memory);
  }

  // Translates the given program into a vector of BfOps that can be used for fast
  // interpretation.
  private static List<BfOp> translate_program(string instructions) {
    int pc = 0;
    int program_size = instructions.Length;
    List<BfOp> ops = new List<BfOp>();

    // Throughout the translation loop, this stack contains offsets (in the ops
    // vector) of open brackets (JUMP_IF_DATA_ZERO ops) waiting for a closing
    // bracket. Since brackets nest, these naturally form a stack. The
    // JUMP_IF_DATA_ZERO ops get added to ops with their argument set to 0 until a
    // matching closing bracket is encountered, at which point the argument can be
    // back-patched.
    Stack<int> open_bracket_stack = new Stack<int>();

    while (pc < program_size) {
      char instruction = instructions[pc];
      if (instruction == '[') {
        // Place a jump op with a placeholder 0 offset. It will be patched-up to
        // the right offset when the matching ']' is found.
        open_bracket_stack.Push(ops.Count);
        ops.Add(new BfOp(BfOpKind.JUMP_IF_DATA_ZERO, 0));
        pc++;
      } else if (instruction == ']') {
        if (open_bracket_stack.Count == 0) {
          BfUtil.DIE($"unmatched closing ']' at pc={pc}");
        }
        int open_bracket_offset = open_bracket_stack.Pop();

        // Try to optimize this loop; if optimize_loop succeeds, it returns a
        // non-empty vector which we can splice into ops in place of the loop.
        // If the returned vector is empty, we proceed as usual.
        List<BfOp> optimized_loop = optimize_loop(ops, open_bracket_offset);

        if (optimized_loop.Count == 0) {
          // Loop wasn't optimized, so proceed emitting the back-jump to ops. We
          // have the offset of the matching '['. We can use it to create a new
          // jump op for the ']' we're handling, as well as patch up the offset of
          // the matching '['.
          ops[open_bracket_offset].argument = ops.Count;
          ops.Add(new BfOp(BfOpKind.JUMP_IF_DATA_NOT_ZERO, open_bracket_offset));
        } else {
          // Replace this whole loop with optimized_loop.
          ops.RemoveRange(open_bracket_offset, ops.Count - open_bracket_offset);
          ops.AddRange(optimized_loop);
        }
        pc++;
      } else {
        // Not a jump; all the other ops can be repeated, so find where the repeat
        // ends.
        int start = pc++;
        while (pc < program_size && instructions[pc] == instruction) {
          pc++;
        }
        // Here pc points to the first new instruction encountered, or to the end
        // of the program.
        int num_repeats = pc - start;

        // Figure out which op kind the instruction represents and add it to the
        // ops.
        BfOpKind kind = BfOpKind.INVALID_OP;
        switch (instruction) {
        case '>':
          kind = BfOpKind.INC_PTR;
          break;
        case '<':
          kind = BfOpKind.DEC_PTR;
          break;
        case '+':
          kind = BfOpKind.INC_DATA;
          break;
        case '-':
          kind = BfOpKind.DEC_DATA;
          break;
        case ',':
          kind = BfOpKind.READ_STDIN;
          break;
        case '.':
          kind = BfOpKind.WRITE_STDOUT;
          break;
        default:
          BfUtil.DIE($"bad char '{instruction}' at pc={start}");
          break;
        }

        ops.Add(new BfOp(kind, num_repeats));
      }
    }

    return ops;
  }

  // Optimizes a loop that starts at loop_start (the opening JUMP_IF_DATA_ZERO).
  // The loop runs until the end of ops (implicitly there's a back-jump after the
  // last op in ops).
  //
  // If optimization succeeds, returns a sequence of instructions that replace the
  // loop; otherwise, returns an empty vector.
  private static List<BfOp> optimize_loop(List<BfOp> ops, int loop_start) {
    List<BfOp> new_ops = new List<BfOp>();

    if (ops.Count - loop_start == 2) {
      BfOp repeated_op = ops[loop_start + 1];
      if (repeated_op.kind == BfOpKind.INC_DATA ||
          repeated_op.kind == BfOpKind.DEC_DATA) {
        new_ops.Add(new BfOp(BfOpKind.LOOP_SET_TO_ZERO, 0));
      } else if (repeated_op.kind == BfOpKind.INC_PTR ||
                 repeated_op.kind == BfOpKind.DEC_PTR) {
        new_ops.Add(new BfOp(BfOpKind.LOOP_MOVE_PTR, repeated_op.kind == BfOpKind.INC_PTR
                             ? repeated_op.argument
                             : -repeated_op.argument));
      }
    } else if (ops.Count - loop_start == 5) {
      // Detect patterns: -<+> and ->+<
      if (ops[loop_start + 1].kind == BfOpKind.DEC_DATA &&
          ops[loop_start + 3].kind == BfOpKind.INC_DATA &&
          ops[loop_start + 1].argument == 1 &&
          ops[loop_start + 3].argument == 1) {

        if (ops[loop_start + 2].kind == BfOpKind.INC_PTR &&
            ops[loop_start + 4].kind == BfOpKind.DEC_PTR &&
            ops[loop_start + 2].argument == ops[loop_start + 4].argument) {
          new_ops.Add(new BfOp(BfOpKind.LOOP_MOVE_DATA, ops[loop_start + 2].argument));
        } else if (ops[loop_start + 2].kind == BfOpKind.DEC_PTR &&
                   ops[loop_start + 4].kind == BfOpKind.INC_PTR &&
                   ops[loop_start + 2].argument == ops[loop_start + 4].argument) {
          new_ops.Add(new BfOp(BfOpKind.LOOP_MOVE_DATA, -ops[loop_start + 2].argument));
        }
      }
    }
    return new_ops;
  }
}
