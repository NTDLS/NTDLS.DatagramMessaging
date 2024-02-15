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
        private readonly Dictionary<Type, MethodInfo> _methodCache = new();
        private readonly Dictionary<Type, IDmMessageHandler> _instanceCache = new();

        internal void AddInstance(IDmMessageHandler handlerClass)
        {
            _instanceCache.Add(handlerClass.GetType(), handlerClass);

            CacheConventionBasedEventingMethods(handlerClass);
        }

        internal bool GetCachedMethod(Type type, [NotNullWhen(true)] out MethodInfo? cachedMethod)
        {
            if (_methodCache.TryGetValue(type, out cachedMethod) == false)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was not found in the assembly cache.");
            }

            if (cachedMethod?.DeclaringType == null)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was found, but it is not in class that can be instantiated.");
            }

            return true;
        }

        internal bool GetCachedInstance(MethodInfo cachedMethod, [NotNullWhen(true)] out IDmMessageHandler? cachedInstance)
        {
            if (cachedMethod.DeclaringType == null)
            {
                cachedInstance = null;
                return false;
                //throw new Exception($"The handler function '{cachedMethod.Name}' does not have a container class.");
            }

            if (_instanceCache.TryGetValue(cachedMethod.DeclaringType, out cachedInstance))
            {
                return true;
            }

            cachedInstance = Activator.CreateInstance(cachedMethod.DeclaringType) as IDmMessageHandler;
            if (cachedInstance == null)
            {
                return false;
                //throw new Exception($"Failed to instantiate container class '{cachedMethod.DeclaringType.Name}' for handler function '{cachedMethod.Name}'.");
            }
            _instanceCache.Add(cachedMethod.DeclaringType, cachedInstance);

            return true;
        }

        internal void CacheConventionBasedEventingMethods(IDmMessageHandler handlerClass)
        {
            foreach (var method in handlerClass.GetType().GetMethods())
            {
                var parameters = method.GetParameters();
                if (parameters.Count() == 2)
                {
                    //Notification prototype: void HandleMyNotification(IReliableMessagingEndpoint endpoint, Guid connectionId, MyNotification notification)
                    //Query prototype:        IReliableMessagingQueryReply HandleMyQuery(IReliableMessagingEndpoint endpoint, Guid connectionId, MyQuery query)

                    if (typeof(DmContext).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    if (typeof(IDmPayload).IsAssignableFrom(parameters[1].ParameterType) == false)
                    {
                        continue;
                    }

                    var payloadParameter = parameters[1];
                    if (payloadParameter != null)
                    {
                        _methodCache.Add(payloadParameter.ParameterType, method);
                    }
                }
            }
        }
    }
}
