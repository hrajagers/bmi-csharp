﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;

namespace BasicModelInterface.Reflection
{
    /// <summary>
    /// Original source: https://code.google.com/p/dynamicdllimport/
    /// </summary>
    public class DynamicDllImport : DynamicObject
    {
        public CallingConvention CallingConvention = CallingConvention.Cdecl;
        public CharSet CharSet = CharSet.Auto;
        private AssemblyBuilder assemblyBuilder;
        private string dllFileName;
        private int methodIndex;
        private ModuleBuilder moduleBuilder;
        private string dllDirectory;
        private Dictionary<string, MethodInfo> methodInfos = new Dictionary<string, MethodInfo>(); 

        public DynamicDllImport(string dllName)
        {
            this.dllFileName = dllName;
        }

        public DynamicDllImport(string dllPath, CharSet charSet = CharSet.Auto,
            CallingConvention callingConvention = CallingConvention.Cdecl)
        {
            this.DllPath = dllPath;
            dllDirectory = Path.GetDirectoryName(dllPath);
            dllFileName = Path.GetFileNameWithoutExtension(dllPath);

            CharSet = charSet;
            CallingConvention = callingConvention;
        }

        public string DllPath { get; private set; }

        public override DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new DynamicDllImportMetaObject(parameter, this);
        }

        public MethodInfo GetInvokeMethod(string methodName, Type returnType, Type[] types)
        {
            if (assemblyBuilder == null)
            {
                var assemblyName = dllFileName + "CSharp";
                assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave, dllDirectory);
                moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName, assemblyName + ".dll");
            }

            MethodInfo methodInfo = null;
            methodInfos.TryGetValue(methodName, out methodInfo);

            if (methodInfo == null)
            {

                var typeBuilder = moduleBuilder.DefineType("NativeLibrary" + methodName,
                    TypeAttributes.Public | TypeAttributes.UnicodeClass);

                var methodBuilder = typeBuilder.DefinePInvokeMethod(methodName, DllPath,
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
                    CallingConventions.Standard,
                    returnType, types,
                    CallingConvention, CharSet);

                methodBuilder.SetImplementationFlags(methodBuilder.GetMethodImplementationFlags() |
                                                     MethodImplAttributes.PreserveSig);

                var type = typeBuilder.CreateType();

                methodInfo = type.GetMethod(methodName);

                methodInfos[methodName] = methodInfo;
            }

            return methodInfo;
        }
    }
}