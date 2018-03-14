using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

public interface IBfProgram {
  void Invoke(byte[] memory);
}

class BracketLabels {
  public readonly Label openLabel;
  public readonly Label closeLabel;

  public BracketLabels(Label ol, Label cl) {
    openLabel = ol;
    closeLabel = cl;
  }
};

public class BfGen {
  public IBfProgram Gen(string instructions) {
    var className = "CBf";
    AssemblyBuilder theAssembly = EmitAssembly(className, instructions);
    //theAssembly.Save("MBf.dll");
    return (IBfProgram)theAssembly.CreateInstance(className, false, BindingFlags.ExactBinding, null,
                                                  new Object[] {}, null, null);
  }

  private AssemblyBuilder EmitAssembly(string className, string instructions) {
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

    MethodBuilder simpleMethod = DefineInvokeMethod(myType, instructions);
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

  private MethodBuilder DefineInvokeMethod(TypeBuilder myType, string instructions) {
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

    generator.Emit(OpCodes.Ldc_I4_0);
    generator.Emit(OpCodes.Stloc, dataptr);  // dataptr = 0

    for (int pc = 0; pc < instructions.Length; ++pc) {
      char c = instructions[pc];
      switch (c) {
      case '>':
        generator.Emit(OpCodes.Ldloc, dataptr);
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Stloc, dataptr);  // ++dataptr
        break;
      case '<':
        generator.Emit(OpCodes.Ldloc, dataptr);
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Sub);
        generator.Emit(OpCodes.Stloc, dataptr);  // --dataptr
        break;
      case '+':
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] += 1
        break;
      case '-':
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Sub);
        generator.Emit(OpCodes.Stelem_I4);  // memory[pc] -= 1
        break;
      case '.':
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
        generator.EmitCall(OpCodes.Call, putcharMI, null);  // putchar(memory[dataptr])
        break;
      case ',':
        generator.Emit(OpCodes.Ldarg_1);  // memory
        generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
        generator.EmitCall(OpCodes.Call, getcharMI, null);  // getchar()
        generator.Emit(OpCodes.Stelem_I4);  // memory[dataptr] = getchar()
        break;
      case '[':
        {
          Label openLabel = generator.DefineLabel();
          Label closeLabel = generator.DefineLabel();
          generator.Emit(OpCodes.Ldarg_1);  // memory
          generator.Emit(OpCodes.Ldloc, dataptr);  // dataptr
          generator.Emit(OpCodes.Ldelem_I4);  // memory[dataptr]
          generator.Emit(OpCodes.Ldc_I4, 255);
          generator.Emit(OpCodes.And);
          generator.Emit(OpCodes.Ldc_I4_0);  // 0
          generator.Emit(OpCodes.Beq, closeLabel);  // if memory[pc] == 0 goto closeLabel
          generator.MarkLabel(openLabel);
          openBracketStack.Push(new BracketLabels(openLabel, closeLabel));
        }
        break;
      case ']':
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
          generator.Emit(OpCodes.Bne_Un, labels.openLabel);  // if memory[pc] != 0 goto openLabel
          generator.MarkLabel(labels.closeLabel);
        }
        break;
      default:
        Console.WriteLine("Unhandled instruction: " + c);
        break;
      }
    }

    generator.Emit(OpCodes.Ret);

    return simpleMethod;
  }
}

public class BfJit {
  public static void Main(string[] args) {
    if (args.Length < 1) {
      BfUtil.DIE("argv < 1");
    }

    string bfCode = BfUtil.LoadProgram(args[0]);
    //Console.WriteLine(bfCode);

    var gen = new BfGen();
    var program = gen.Gen(bfCode);
    byte[] memory = new byte[30000];
    program.Invoke(memory);
  }
}
