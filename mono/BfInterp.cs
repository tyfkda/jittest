using System;

public class BfInterp {
  public static void Run(byte[] memory, string instructions) {
    int pc = 0;
    int dataptr = 0;

    while (pc < instructions.Length) {
      char instruction = instructions[pc];
      switch (instruction) {
      case '>':
        dataptr++;
        break;
      case '<':
        dataptr--;
        break;
      case '+':
        memory[dataptr]++;
        break;
      case '-':
        memory[dataptr]--;
        break;
      case '.':
        BfUtil.PutChar((char)memory[dataptr]);
        break;
      case ',':
        memory[dataptr] = (byte)BfUtil.GetChar();
        break;
      case '[':
        if (memory[dataptr] == 0) {
          int bracket_nesting = 1;
          int saved_pc = pc;

          while (bracket_nesting > 0 && ++pc < instructions.Length) {
            if (instructions[pc] == ']') {
              bracket_nesting--;
            } else if (instructions[pc] == '[') {
              bracket_nesting++;
            }
          }

          if (bracket_nesting == 0) {
            break;
          } else {
            BfUtil.DIE($"unmatched '[' at pc={saved_pc}");
          }
        }
        break;
      case ']':
        if (memory[dataptr] != 0) {
          int bracket_nesting = 1;
          int saved_pc = pc;

          while (bracket_nesting > 0 && pc > 0) {
            pc--;
            if (instructions[pc] == '[') {
              bracket_nesting--;
            } else if (instructions[pc] == ']') {
              bracket_nesting++;
            }
          }

          if (bracket_nesting == 0) {
            break;
          } else {
            BfUtil.DIE($"unmatched ']' at pc={saved_pc}");
          }
        }
        break;
      default:
        BfUtil.DIE($"bad char '{instruction}' at pc={pc}");
        break;
      }

      pc++;
    }
  }

  public static void Main(string[] args) {
    if (args.Length < 1) {
      BfUtil.DIE("argv < 1");
    }

    string bfCode = BfUtil.LoadProgram(args[0]);

    byte[] memory = new byte[30000];
    Run(memory, bfCode);
  }
}
