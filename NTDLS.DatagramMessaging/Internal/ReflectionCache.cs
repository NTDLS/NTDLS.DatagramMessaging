using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace NTDLS.DatagramMessaging.Internal
{
    /// <summary>
    /// Manages class instances and method reflection information for message handlers.
    /// </summary>
    public class ReflectionCache
    {
        /// <summary>
        /// Determines the type of method which will be executed.
        /// </summary>
        internal enum CachedMethodType
        {
            /// <summary>
            /// The hander function has only a datagram parameter.
            /// </summary>
            DatagramOnly,
            /// <summary>
            /// The hander function has both a context and a datagram parameter.
            /// </summary>
            DatagramWithContext
        }

        /// <summary>
        /// An instance of a cached method.
        /// </summary>
        internal class CachedMethod
        {
            /// <summary>
            /// The reflection instance of the cached method.
            /// </summary>
            public MethodInfo Method { get; private set; }

            /// <summary>
            /// The type of the function.
            /// </summary>
            public CachedMethodType MethodType { get; private set; }

            /// <summary>
            /// Creates a new instance of the CachedMethod class.
            /// </summary>
            public CachedMethod(CachedMethodType methodType, MethodInfo method)
            {
                MethodType = methodType;
                Method = method;
            }
        }

        private readonly Dictionary<string, CachedMethod> _handlerMethods = new();
        private readonly Dictionary<Type, IDmDatagramHandler> _handlerInstances = new();

        internal void AddInstance(IDmDatagramHandler handler)
        {
            _handlerInstances.Add(handler.GetType(), handler);

            LoadConventionBasedHandlerMethods(handler);
        }

        /// <summary>
        /// Calls the appropriate handler function for the given datagram.
        /// </summary>
        /// <returns>Returns true if the function was found and executed.</returns>
        internal bool RouteToDatagramHander(DmContext context, IDmDatagram datagram)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnDatagramReceived() event.
            if (GetCachedMethod(datagram, out var cachedMethod))
            {
                if (GetCachedInstance(cachedMethod, out var cachedInstance))
                {
                    var method = MakeGenericMethodForDatagram(cachedMethod, datagram);

                    switch (cachedMethod.MethodType)
                    {
                        case CachedMethodType.DatagramOnly:
                            method.Invoke(cachedInstance, [datagram]);
                            return true;
                        case CachedMethodType.DatagramWithContext:
                            method.Invoke(cachedInstance, [context, datagram]);
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a cacheable and invokable instance of a handler function by matching generic argument types.
        /// </summary>
        private static MethodInfo MakeGenericMethodForDatagram(CachedMethod cachedMethod, IDmDatagram datagram)
        {
            var datagramType = datagram.GetType();

            if (DmCaching.TryGet<MethodInfo>(datagramType, out var cached) && cached != null)
            {
                return cached;
            }

            if (datagramType.IsGenericType && cachedMethod.Method.IsGenericMethod == true)
            {
                //If both the datagram and the handler function are generic, We need to create a
                //  generic version of the handler function using the generic types of the datagram.

                // Get the generic type definition and its assembly name
                var typeDefinitionName = datagramType.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = datagramType.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                // Recursively get the AssemblyQualifiedName of generic arguments
                var genericTypeArguments = datagramType.GetGenericArguments()
                    .Select(t => Type.GetType(t.AssemblyQualifiedName ?? Reflection.GetAssemblyQualifiedTypeName(t))
                     ?? throw new Exception($"The generic assembly type [{t.AssemblyQualifiedName}] could not be instantiated.")
                    ).ToArray();

                if (genericTypeArguments == null)
                {
                    throw new Exception("The generic assembly type could not be instantiated.");
                }

                var genericMethod = cachedMethod.Method.MakeGenericMethod(genericTypeArguments)
                    ?? throw new Exception("The generic assembly type could not be instantiated.");

                DmCaching.SetOneMinute(datagramType, genericMethod);

                return genericMethod;
            }
            else
            {
                return cachedMethod.Method;
            }
        }

        /// <summary>
        /// Gets the handler class instance from the pre-loaded handler instance cache.
        /// </summary>
        private bool GetCachedInstance(CachedMethod cachedMethod, [NotNullWhen(true)] out IDmDatagramHandler? cachedInstance)
        {
            if (cachedMethod.Method.DeclaringType == null)
            {
                cachedInstance = null;
                return false;
            }

            if (_handlerInstances.TryGetValue(cachedMethod.Method.DeclaringType, out cachedInstance))
            {
                return true;
            }

            cachedInstance = Activator.CreateInstance(cachedMethod.Method.DeclaringType) as IDmDatagramHandler;
            if (cachedInstance == null)
            {
                return false;
            }
            _handlerInstances.Add(cachedMethod.Method.DeclaringType, cachedInstance);

            return true;
        }

        /// <summary>
        /// Gets the handler function from the pre-loaded handler function cache.
        /// </summary>
        private bool GetCachedMethod(IDmDatagram datagram, [NotNullWhen(true)] out CachedMethod? cachedMethod)
        {
            var typeName = Reflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(datagram);

            if (_handlerMethods.TryGetValue(typeName, out cachedMethod) == false)
            {
                return false;
            }

            if (cachedMethod.Method.DeclaringType == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads the handler functions from the given handler class.
        /// </summary>
        private void LoadConventionBasedHandlerMethods(IDmDatagramHandler handlerClass)
        {
            foreach (var method in handlerClass.GetType().GetMethods())
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                {
                    if (typeof(IDmDatagram).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    var datagramParameter = parameters[0];
                    if (datagramParameter != null)
                    {
                        var datagramParameterTypeName = Reflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(datagramParameter.ParameterType);
                        _handlerMethods.Add(datagramParameterTypeName, new CachedMethod(CachedMethodType.DatagramOnly, method));
                    }
                }
                else if (parameters.Length == 2)
                {
                    if (typeof(DmContext).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    if (typeof(IDmDatagram).IsAssignableFrom(parameters[1].ParameterType) == false)
                    {
                        continue;
                    }

                    var datagramParameter = parameters[1];
                    if (datagramParameter != null)
                    {
                        var datagramParameterTypeName = Reflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(datagramParameter.ParameterType);
                        _handlerMethods.Add(datagramParameterTypeName, new CachedMethod(CachedMethodType.DatagramWithContext, method));
                    }
                }
            }
        }
    }
}
