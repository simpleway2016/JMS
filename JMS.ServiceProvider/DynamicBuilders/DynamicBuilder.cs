using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Schema;

namespace JMS.DynamicBuilders
{
    public class DynamicBuilder
    {
        /// <summary>
        /// 为controller每个方法生成一个IDynamicInovker（controller必须是public，否则方法会调用失败）
        /// </summary>
        /// <param name="controllerType"></param>
        /// <returns></returns>
        public static Dictionary<string, IDynamicInovker> Build(Type controllerType)
        {
            var methods = controllerType.GetTypeInfo().DeclaredMethods.Where(m => m.IsPublic && !m.IsStatic && m.DeclaringType.IsSubclassOf(typeof(MicroServiceControllerBase))).ToArray();
            var dict = new Dictionary<string, IDynamicInovker>();

            foreach (var method in methods)
            {
                AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);

                ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicInovkerModule");
                TypeBuilder typeBuilder = moduleBuilder.DefineType($"DynamicInovker{Guid.NewGuid().ToString("N")}", TypeAttributes.Public);
                //*添加所实现的接口
                typeBuilder.AddInterfaceImplementation(typeof(IDynamicInovker));

                //实现方法
                MethodBuilder mbIM = typeBuilder.DefineMethod(nameof(IDynamicInovker.Invoke),
               MethodAttributes.Public | MethodAttributes.HideBySig |
                   MethodAttributes.NewSlot | MethodAttributes.Virtual |
                   MethodAttributes.Final,
               typeof(object),
               new Type[] { typeof(MicroServiceControllerBase),  typeof(object[]) });


                var equalsMethod = typeof(string).GetTypeInfo().DeclaredMethods.FirstOrDefault(m => m.Name == "op_Equality" && m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(string));


                ILGenerator il = mbIM.GetILGenerator();
                il.DeclareLocal(typeof(object));


                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_1);

                var parameters = method.GetParameters();
                for (int j = 0; j < parameters.Length; j++)
                {
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldc_I4, j);
                    il.Emit(OpCodes.Ldelem_Ref);
                    if (parameters[j].ParameterType.IsValueType)
                    {
                        il.Emit(OpCodes.Unbox_Any, parameters[j].ParameterType);
                    }
                    else
                    {
                        il.Emit(OpCodes.Castclass, parameters[j].ParameterType);

                    }

                }

                il.EmitCall(OpCodes.Callvirt, method, parameters.Select(m => m.ParameterType).ToArray());

                if (method.ReturnType == typeof(void))
                {
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Stloc_0);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    if (method.ReturnType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, method.ReturnType);
                    }
                    il.Emit(OpCodes.Stloc_0);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ret);
                }


                typeBuilder.DefineMethodOverride(mbIM, typeof(IDynamicInovker).GetMethod(nameof(IDynamicInovker.Invoke)));

                Type retType = typeBuilder.CreateType();

                dict[method.Name] = (IDynamicInovker)Activator.CreateInstance(retType);
            }
            return dict;
        }
    }
}
