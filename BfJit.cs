using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

public interface IBfProgram {
  void Invoke();
}

public class BfGen {
  public IBfProgram Gen() {
    var className = "CBf";
    Assembly theAssembly = EmitAssembly(className);
    return (IBfProgram)theAssembly.CreateInstance(className);
  }

  private Assembly EmitAssembly(string className) {
    AssemblyName assemblyName = new AssemblyName();
    assemblyName.Name = "Bf";

    AssemblyBuilder newAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    ModuleBuilder newModule = newAssembly.DefineDynamicModule("MBf");

    TypeBuilder myType = newModule.DefineType(className, TypeAttributes.Public);

    myType.AddInterfaceImplementation(typeof(IBfProgram));

    MethodBuilder simpleMethod = myType.DefineMethod("Invoke",
                                                     MethodAttributes.Public | MethodAttributes.Virtual,
                                                     typeof(void),  // returnType
                                                     new Type[0]);  // ;paramTypes

    ILGenerator generator = simpleMethod.GetILGenerator();

    MethodInfo writeLineMI = typeof(BfJit).GetMethod("Callback",
                                                     new Type[] {typeof(int)});

    generator.Emit(OpCodes.Ldc_I4, 1234);
    generator.EmitCall(OpCodes.Call, writeLineMI, null);
    generator.Emit(OpCodes.Ret);

    myType.DefineMethodOverride(simpleMethod, typeof(IBfProgram).GetMethod("Invoke"));

    myType.CreateType();
    return newAssembly;
  }
}

public class BfJit {
  public static void Main(string[] args) {
    var gen = new BfGen();
    Console.WriteLine("Gen");
    var program = gen.Gen();
    Console.WriteLine("Invoke");
    program.Invoke();
    Console.WriteLine("Done");
  }

  public static void Callback(int param) {
    Console.WriteLine("Called: " + param);
  }
}
