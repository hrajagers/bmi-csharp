﻿using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BasicModelInterface
{
    /// <summary>
    ///     Loads <paramref name="libraryPath" /> as a dynamic library and redirects all <see cref="IBasicModelInterface" />.
    /// </summary>
    public class BasicModelInterfaceLibrary : IBasicModelInterface
    {
        public const int MAXDIMS = 6;

        public const int MAXSTRLEN = 1024;

        private string originalCurrentDirectory;

        private dynamic lib;
        
        private string[] variableNames;

        /// <summary>
        /// Run model in one step from start to end.
        /// </summary>
        /// <param name="library"></param>
        /// <param name="configPath"></param>
        public static void Run(string library, string configPath)
        {
            var model = new BasicModelInterfaceLibrary(library);

            model.Initialize(configPath);

            var t = model.StartTime;
            while (t < model.StopTime)
            {
                t = model.CurrentTime;
                model.Update(-1);
            }

            model.Finish();
        }

        public BasicModelInterfaceLibrary(string libraryPath, CallingConvention callingConvention = CallingConvention.Cdecl)
        {
            lib = new DynamicDllImport(libraryPath, CharSet.Ansi, callingConvention);
        }

        public DateTime StartTime
        {
            get
            {
                double t = 0.0;
                lib.get_start_time(ref t);
                return new DateTime().AddSeconds(t);
            }
        }

        public DateTime StopTime
        {
            get
            {
                var t = 0.0;
                lib.get_end_time(ref t);
                return new DateTime().AddSeconds(t);
            }
        }

        public DateTime CurrentTime
        {
            get
            {
                var t = 0.0;
                lib.get_current_time(ref t);
                return new DateTime().AddSeconds(t);
            }
        }

        public TimeSpan TimeStep { get; private set; }

        public void Initialize(string path)
        {
            originalCurrentDirectory = Directory.GetCurrentDirectory();

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.SetCurrentDirectory(dir);
            }

            var configFile = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(configFile))
            {
                configFile = configFile.PadRight(MAXSTRLEN, '\0'); // make FORTRAN friendly
            }

            lib.initialize(configFile);
        }

        public void Update(double timeStep)
        {
            lib.update(ref timeStep);
        }

        public void Finish()
        {
            lib.finalize();

            if (string.IsNullOrEmpty(originalCurrentDirectory))
            {
                return;
            }

            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }

        public string[] VariableNames
        {
            get
            {
                if (variableNames == null)
                {
                    GetVariableNames();
                }

                return variableNames;
            }
        }

        public IArray<T> GetValues<T>(string variable)
        {
            // get values (pointer)
            int values = 0;
            lib.get_var_values(variable, ref values);

            // get rank
            int rank = 0;
            lib.get_var_rank(variable, ref rank);

            if (rank > 1)
            {
                throw new NotImplementedException("Only variables with rank 1 are supported");
            }

            // get shape
            var shape = new int[MAXDIMS];
            lib.get_var_shape(variable, shape);

            //return new NativeArray<T>(values, rank, shape);
            return null;
        }

        public void SetValues<T>(string variable, IArray<T> values)
        {
            throw new NotImplementedException();
        }

        public int[] GetIntValues1D(string variable)
        {
            int rank = 0;
            lib.get_var_rank(variable, ref rank);

            if (rank > 1)
            {
                throw new NotImplementedException("Only variables with rank 1 are supported");
            }

            var shape = new int[MAXDIMS];
            lib.get_var_shape(variable, shape);

            if (rank == 1)
            {
                var valuesPointer = new IntPtr();
                lib.get_1d_int(variable, ref valuesPointer);
                int length = shape[0];
                var values = new int[length];
                if (length > 0)
                {
                    Marshal.Copy(valuesPointer, values, 0, length);
                }

                return values;
            }

            return null;
        }

        public int[,] GetIntValues2D(string variable)
        {
            int rank = 0;
            lib.get_var_rank(variable, ref rank);

            if (rank > 2)
            {
                throw new NotImplementedException("Only variables with rank 1 are supported");
            }

            var shape = new int[MAXDIMS];
            lib.get_var_shape(variable, shape);

            if (rank == 2)
            {
                var valuesPointer = new IntPtr();
                lib.get_2d_int(variable, ref valuesPointer);

                lib.get_var_shape(variable, shape);

                int length = shape[0]*shape[1];
                var values = new int[length];
                Marshal.Copy(valuesPointer, values, 0, length);

                // TODO: optimize this, avoid double copy
                var values2d = new int[shape[0], shape[1]];
                for (var i = 0; i < shape[0]; i++)
                {
                    for (var j = 0; j < shape[1]; j++)
                    {
                        values2d[i, j] = values[i * shape[0] + j];
                    }
                }

                return values2d;
            }

            return new int[,] {};
        }

        public double[] GetDoubleValues1D(string variable)
        {
            int rank = 0;
            lib.get_var_rank(variable, ref rank);

            if (rank > 1)
            {
                throw new NotImplementedException("Only variables with rank 1 are supported");
            }

            var shape = new int[MAXDIMS];
            lib.get_var_shape(variable, shape);

            if (rank == 1)
            {
                var valuesPointer = new IntPtr();
                lib.get_1d_double(variable, ref valuesPointer);
                int length = shape[0];
                var values = new double[length];
                if (length > 0)
                {
                    Marshal.Copy(valuesPointer, values, 0, length);
                }

                return values;
            }

            return null;
        }

        public void SetDoubleValue1DAtIndex(string variable, int index, double valueDouble)
        {
            lib.set_1d_double_at_index(variable, ref index, ref valueDouble);
        }

        private void GetVariableNames()
        {
            var count = 0;
            lib.get_var_count(ref count);

            var strings = new string[count];
            for (var i = 0; i < count; i++)
            {
                var variableNameBuffer = new StringBuilder(MAXSTRLEN);
                lib.get_var_name(ref i, variableNameBuffer);
                strings[i] = variableNameBuffer.ToString();
            }

            variableNames = strings;
        }
    }
}