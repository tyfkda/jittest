using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

public interface IBfProgram {
  void Invoke();
}

public class BfGen {
  public IBfProgram Gen(char[] memory) {
    var className = "CBf";
    Assembly theAssembly = EmitAssembly(className);
    return (IBfProgram)theAssembly.CreateInstance(className, false, BindingFlags.ExactBinding, null,
                                                  new Object[] {memory}, null, null);
  }

  private Assembly EmitAssembly(string className) {
    AssemblyName assemblyName = new AssemblyName();
    assemblyName.Name = "Bf";

    AssemblyBuilder newAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    ModuleBuilder newModule = newAssembly.DefineDynamicModule("MBf");

    TypeBuilder myType = newModule.DefineType(className, TypeAttributes.Public);

    // Add a private field of type int (Int32).
    FieldBuilder fbMemory = myType.DefineField("memory",
                                               typeof(char[]),
                                               FieldAttributes.Private);

    myType.AddInterfaceImplementation(typeof(IBfProgram));

    // Define constructor
    DefineConstructor(myType, fbMemory);

    MethodBuilder simpleMethod = myType.DefineMethod("Invoke",
                                                     MethodAttributes.Public | MethodAttributes.Virtual,
                                                     typeof(void),  // returnType
                                                     new Type[0]);  // ;paramTypes

    ILGenerator generator = simpleMethod.GetILGenerator();

    MethodInfo callbackMI = typeof(BfJit).GetMethod("Callback",
                                                    new Type[] {typeof(int)});

    generator.DeclareLocal(typeof(int));

    generator.Emit(OpCodes.Ldarg_0);  // this
    generator.Emit(OpCodes.Ldfld, fbMemory);  // this.memory
    generator.Emit(OpCodes.Ldc_I4, 0);
    generator.Emit(OpCodes.Ldelem_I4);  // this.memory[0]
    generator.Emit(OpCodes.Ldc_I4, 1);
    generator.Emit(OpCodes.Add);
    generator.Emit(OpCodes.Stloc_0);  // local0 = this.memory[0] + 1

    generator.Emit(OpCodes.Ldarg_0);  // this
    generator.Emit(OpCodes.Ldfld, fbMemory);  // this.memory
    generator.Emit(OpCodes.Ldc_I4, 0);
    generator.Emit(OpCodes.Ldloc_0);
    generator.Emit(OpCodes.Stelem_I4);  // local0 = this.memory[0] + 1

    generator.Emit(OpCodes.Ldloc_0);
    generator.EmitCall(OpCodes.Call, callbackMI, null);  // callback(local0)

    generator.Emit(OpCodes.Ret);

    myType.DefineMethodOverride(simpleMethod, typeof(IBfProgram).GetMethod("Invoke"));

    myType.CreateType();

    return newAssembly;
  }

  private void DefineConstructor(TypeBuilder myType, FieldBuilder fbMemory) {
    ConstructorBuilder ctor = myType.DefineConstructor(MethodAttributes.Public,
                                                       CallingConventions.Standard,
                                                       new Type[] { typeof(char[]) });
    ILGenerator generator = ctor.GetILGenerator();
    generator.Emit(OpCodes.Ldarg_0);  // this
    generator.Emit(OpCodes.Ldarg_1);  // argument
    generator.Emit(OpCodes.Stfld, fbMemory);  // this.fbMemory = argument
    generator.Emit(OpCodes.Ret);
  }
}

public class BfJit {
  public static void Main(string[] args) {
    char[] memory = new char[256];
    var gen = new BfGen();
    Console.WriteLine("Gen");
    var program = gen.Gen(memory);
    Console.WriteLine("Invoke");
    for (int i = 0; i < 10; ++i)
      program.Invoke();
    Console.WriteLine("Done");
  }

  public static void Callback(int param) {
    Console.WriteLine("Called: " + param);
  }
}
