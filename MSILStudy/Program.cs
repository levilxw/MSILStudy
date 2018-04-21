using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace MSILStudy
{
    class Program
    {
        static void Main(string[] args)
        {
            var p = new Person();
            p.SayHi("YY");

            SayHiWithIL("Wuli YY");

            int result;
            var math = new MyMath();
            var count = 10000000;

            Console.WriteLine("数据量：" + count);
            Console.WriteLine("-----------------------------\r\n");

            using (Profiler.Step("循环：{0} ms"))
            {
                for (int i = 0; i < count; i++)
                    result = i;
            }

            using (Profiler.Step("直接调用 ：{0} ms"))
            {
                for (int i = 0; i < count; i++)
                    result = math.Add(i, i);
            }

            using (Profiler.Step("Emit：{0} ms"))
            {
                var emitAdd = BuildEmitAddFunc();
                for (int i = 0; i < count; i++)
                    result = emitAdd(math, i, i);
            }

            using (Profiler.Step("表达式树：{0} ms"))
            {
                var expressionAdd = BuildExpressionAddFunc();
                for (int i = 0; i < count; i++)
                    result = expressionAdd(math, i, i);
            }

            using (Profiler.Step("dynamic 调用：{0} ms"))
            {
                dynamic d = math;
                for (int i = 0; i < count; i++)
                    result = d.Add(i, i);
            }

            using (Profiler.Step("反射调用：{0} ms"))
            {
                var add = typeof(MyMath).GetMethod("Add");
                for (int i = 0; i < count; i++)
                    result = (int)add.Invoke(math, new object[] { i, i });
            }

            Console.ReadLine();
        }

        static void SayHiWithIL(string someOne)
        {
            AssemblyName assemblyName = new AssemblyName("MSILStudy.MyMapper");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            //assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MSILStudyProvider");
            var implementTypeName = "MSILStudy.MyMapper." + nameof(Person);
            var typeBuilder = moduleBuilder.DefineType(implementTypeName, TypeAttributes.Public, typeof(object));

            // define constructor 
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var ilGen = constructorBuilder.GetILGenerator();
            Type objType = typeof(object);
            ConstructorInfo objCtor = objType.GetConstructor(Type.EmptyTypes);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Call, objCtor);
            //ilGen.Emit(OpCodes.Nop);
            ilGen.Emit(OpCodes.Ret);

            var methodBuilder = typeBuilder.DefineMethod("SayHi", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), new Type[] { typeof(string) });
            var ilMethodGen = methodBuilder.GetILGenerator();
            ilMethodGen.Emit(OpCodes.Ldstr, "Nice to meet you, {0}!");
            ilMethodGen.Emit(OpCodes.Ldarg_1);
            ilMethodGen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string), typeof(object) }, null));
            ilMethodGen.Emit(OpCodes.Ret);

            var mapperType = typeBuilder.CreateType();

            //var param0 = Expression.Parameter(typeof(IDbConnection));
            var source = Expression.New(mapperType.GetConstructor(Type.EmptyTypes));
            var expr = Expression.Lambda(source).Compile();
            var obj = expr.DynamicInvoke();


            var param0 = Expression.Parameter(typeof(string));
            var method = Expression.Call(Expression.Constant(obj), mapperType.GetMethod("SayHi", new Type[] { typeof(string) }), param0);
            var methodExpr = Expression.Lambda<Action<string>>(method, param0);
            methodExpr.Compile().Invoke(someOne);
        }

        static Func<MyMath, int, int, int> BuildExpressionAddFunc()
        {
            var add = typeof(MyMath).GetMethod("Add");
            var math = Expression.Parameter(typeof(MyMath));
            var a = Expression.Parameter(typeof(int), "a");
            var b = Expression.Parameter(typeof(int), "b");
            var body = Expression.Call(math, add, a, b);
            var lambda = Expression.Lambda<Func<MyMath, int, int, int>>(body, math, a, b);
            return lambda.Compile();
        }
        static Func<MyMath, int, int, int> BuildEmitAddFunc()
        {
            var add = typeof(MyMath).GetMethod("Add");
            var dynamicMethod = new DynamicMethod("NewAdd", typeof(int), new[] { typeof(MyMath), typeof(int), typeof(int) });
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Callvirt, add);
            il.Emit(OpCodes.Ret);
            return (Func<MyMath, int, int, int>)dynamicMethod.CreateDelegate(typeof(Func<MyMath, int, int, int>));
        }

    }

    class Person
    {
        public void SayHi(string someOne)
        {
            Console.WriteLine("Nice to meet you, {0}!", someOne);
        }
    }

    public class MyMath
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }

    public class Profiler : IDisposable
    {

        private Stopwatch watch;
        private string message;

        private Profiler(string message)
        {
            this.watch = new Stopwatch();
            this.watch.Start();
            this.message = message;
        }

        public void Dispose()
        {
            watch.Stop();
            Console.WriteLine(message, watch.ElapsedMilliseconds);
            Console.WriteLine();
        }

        public static IDisposable Step(string message)
        {
            return new Profiler(message);
        }
    }
}

