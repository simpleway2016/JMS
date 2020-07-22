using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Linq;

namespace JMS.ExpressionBuilder
{
    /// <summary>
    /// 测试发现构建表达式委托，在运行一些稍微参数多的方法，并没有比反射快多少
    /// </summary>
    class Helper
    {
        class MyResult
        {
            public object result;
            public bool setValue(object v)
            {
                result = v;
                return true;
            }
        }

        static Expression getMethodCallExpression(MethodInfo method, Type controllerType, Expression c, Expression ps)
        {
            var parameters = method.GetParameters();
            Expression[] pExps = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var pinfo = parameters[i];
                var valExp = BinaryExpression.ArrayIndex(ps, Expression.Constant(i));
                if (pinfo.ParameterType != typeof(object))
                    pExps[i] = Expression.Convert(valExp, pinfo.ParameterType);
                else
                    pExps[i] = valExp;
            }
            var exp = Expression.Call(c, controllerType.GetMethod(method.Name), pExps);
            return exp;
        }

        static Expression mergeMethods(Expression nameExp, List<Expression> methodExps, MethodInfo[] methods, Expression resultObjExp, int index)
        {

            var left = methodExps[index];
            Expression body = Expression.Equal(nameExp, Expression.Constant(methods[index].Name));

            left = Expression.Call(resultObjExp, typeof(MyResult).GetMethod("setValue"), left);

            Expression right = null;
            if (index < methodExps.Count - 1)
                right = mergeMethods(nameExp, methodExps, methods, resultObjExp, index + 1);
            else
                right = Expression.Constant(false);

            return Expression.Condition(body, left, right);
        }

        static Func<string, object, object[], MyResult, bool> build(Type controllerType)
        {
            var p1 = Expression.Parameter(typeof(string), "m");
            var p2 = Expression.Parameter(typeof(object), "c");
            var p3 = Expression.Parameter(typeof(object[]), "p");
            var p4 = Expression.Parameter(typeof(MyResult), "r");

            var ctrlExp = Expression.Convert(p2, controllerType);

            List<Expression> callExps = new List<Expression>();
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            methods = methods.Where(m => m.DeclaringType == controllerType).ToArray();
            foreach (var method in methods)
            {
                callExps.Add(getMethodCallExpression(method, controllerType, ctrlExp, p3));
            }
            var body = mergeMethods(p1, callExps, methods, p4, 0);

            var ee2 = Expression.Lambda(body, p1, p2, p3, p4);

            var func = (Func<string, object, object[], MyResult, bool>)ee2.Compile();
            return func;
        }
    }
}
