#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Cube.Replication {
    /// <remarks>Available in: Editor</remarks>
    class RpcPostProcessor : PostProcessor {
        const string SEND_RPC_METHOD_NAME_POSTFIX = "__CUBE_NETWORKING_REMOTE__";
        const string ADD_RPC_TO_RPC_MAP_METHOD_NAME = "__ADD_RPC_TO_RPC_MAP__";

        TypeDefinition _networkBehaviourType;
        TypeDefinition _replicaBehaviourType;

        TypeReference _voidTypeReference;
        TypeReference _objectTypeReference;
        TypeReference _systemTypeTypeReference;

        MethodReference _systemTypeGetTypeFromHandleMethod; // typeof()
        MethodReference _systemTypeGetMethodMethod; //System.Type.GetMethod();

        MethodReference _debugLogErrorMethod;
        MethodReference _dictionaryAddMethod;

        MethodReference _sendRpcMethod;

        PropertyDefinition _isServerProperty;
        PropertyDefinition _isClientProperty;

        FieldReference _replicaBehaviourRpcMap;

        TypeDefinition _rpcTargetType;
        int _rpcTargetServerValue;

        Dictionary<string, byte> _processedTypes = new Dictionary<string, byte>();

        public RpcPostProcessor(ApplicationType app, ModuleDefinition module)
            : base(app, module) {
            var unityEngineAssembly = module.AssemblyResolver.Resolve(new AssemblyNameReference("UnityEngine.CoreModule", new Version()));
            if (unityEngineAssembly == null)
                Debug.LogError("RPC Patcher: Cannot resolve 'UnityEngine'");

            var networkingReplicaAssembly = ResolveNetworkingReplicaAssembly();

            _networkBehaviourType = GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Networking.Replicas.NetworkBehaviour");
            _replicaBehaviourType = GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Networking.Replicas.ReplicaBehaviour");

            var debugType = GetTypeDefinitionByName(unityEngineAssembly.MainModule, "UnityEngine.Debug");
            _debugLogErrorMethod = ImportMethod(GetMethodDefinitionByName(debugType, "LogError"));

            _sendRpcMethod = ImportMethod(GetMethodDefinitionByName(_replicaBehaviourType, "SendRpc"));

            _isServerProperty = GetPropertyDefinitionByName(_networkBehaviourType, "isServer");
            _isClientProperty = GetPropertyDefinitionByName(_networkBehaviourType, "isClient");

            _replicaBehaviourRpcMap = ImportField(GetFieldDefinitionByName(_replicaBehaviourType, "_rpcMethods"));

            _voidTypeReference = module.TypeSystem.Void;
            _objectTypeReference = module.TypeSystem.Object;
            _systemTypeTypeReference = module.ImportReference(typeof(Type));

            _systemTypeGetTypeFromHandleMethod = module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
            _systemTypeGetMethodMethod = module.ImportReference(typeof(Type).GetMethod("GetMethod", new Type[] {
                        typeof(string),
                        typeof(BindingFlags),
                        typeof(Binder),
                        typeof(Type[]),
                        typeof(ParameterModifier[])
                    }));

            _dictionaryAddMethod = module.Assembly.MainModule.ImportReference(typeof(Dictionary<byte, string>).GetMethod("Add"));

            _rpcTargetType = GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Networking.Replicas.RpcTarget");
            _rpcTargetServerValue = GetEnumValueByName(_rpcTargetType, "Server");
        }

        public override bool Process() {
            foreach (var type in module.Types) {
                ProcessType(type);

                foreach (var nestedType in type.NestedTypes) {
                    ProcessType(nestedType);
                }
            }
            return true;
        }

        void ProcessType(TypeDefinition type) {
            if (!type.IsClass || !type.HasMethods)
                return;

            if (_processedTypes.ContainsKey(type.FullName))
                return;

            byte nextRpcMethodId = 0;
            var remoteMethods = new List<MethodDefinition>();

            TypeDefinition baseType = null;
            if (InheritsTypeFrom(type, "Cube.Networking.Replicas.ReplicaBehaviour")) {
                var tmp = ResolveTypeReference(type.BaseType);

                //#TODO classes from other modules (filter "tmp.Module.path" in "Application.dataPath" ?)
                if (tmp.FullName != "Cube.Networking.Replicas.ReplicaBehaviour" && tmp.Module.Name == module.Name) {
                    baseType = tmp;
                    ProcessType(baseType);
                    nextRpcMethodId = _processedTypes[baseType.FullName];
                }
            }

            var rpcMethods = new Dictionary<byte, MethodDefinition>();

            foreach (var method in type.Methods) {
                if (nextRpcMethodId == byte.MaxValue) {
                    Debug.LogError("Reached max RPC method count (" + nextRpcMethodId + ") for type: " + type.FullName + "!");
                    break;
                }

                if (method.IsConstructor || !method.HasBody)
                    continue;

                if (!HasAttribute("Cube.Networking.Replicas.ReplicaRpcAttribute", method))
                    continue;

                var remoteMethod = CreateRpcRemoteMethod(method);
                remoteMethods.Add(remoteMethod);

                CopyMethodBody(method, remoteMethod);
                ClearMethodBody(method);

                if (InjectSendRpcInstructions(type, nextRpcMethodId, method)) {
                    rpcMethods.Add(nextRpcMethodId, method);
                }

                nextRpcMethodId++;
            }

            if (remoteMethods.Count > 0) {
                var addRpcsToMapMethod = CreateAddRpcsToMapMethod(type);

                foreach (var rpcMethod in rpcMethods) {
                    InjectRpcMethodToMap(addRpcsToMapMethod, type, rpcMethod.Key, rpcMethod.Value);
                }

                type.Methods.Add(addRpcsToMapMethod);

                foreach (var method in type.Methods) {
                    if (!method.IsConstructor || method.IsStatic)
                        continue;

                    InjectMethodCallAtEnd(method, addRpcsToMapMethod);
                }
            }

            foreach (var method in remoteMethods) {
                type.Methods.Add(method);
            }

            _processedTypes.Add(type.FullName, nextRpcMethodId);
        }

        MethodDefinition CreateAddRpcsToMapMethod(TypeDefinition type) {
            MethodDefinition addRpcsToMapMethod = GetMethodDefinitionByName(type, ADD_RPC_TO_RPC_MAP_METHOD_NAME);

            if (addRpcsToMapMethod != null)
                ClearMethodBody(addRpcsToMapMethod);
            else
                addRpcsToMapMethod = new MethodDefinition(ADD_RPC_TO_RPC_MAP_METHOD_NAME, Mono.Cecil.MethodAttributes.Private, _voidTypeReference);

            var il = addRpcsToMapMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ret));

            return addRpcsToMapMethod;
        }

        void InjectRpcMethodToMap(MethodDefinition addRpcsToMapMethod, TypeDefinition type, byte methodId, MethodDefinition rpcMethod) {
            var il = addRpcsToMapMethod.Body.GetILProcessor();
            var last = il.Body.Instructions.Last();

            il.InsertBefore(last, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(last, il.Create(OpCodes.Ldfld, _replicaBehaviourRpcMap));

            il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, (int)methodId));

            il.InsertBefore(last, il.Create(OpCodes.Ldtoken, type));
            il.InsertBefore(last, il.Create(OpCodes.Call, _systemTypeGetTypeFromHandleMethod));
            il.InsertBefore(last, il.Create(OpCodes.Ldstr, rpcMethod.Name + SEND_RPC_METHOD_NAME_POSTFIX));
            il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, (int)(BindingFlags.NonPublic | BindingFlags.Instance)));
            il.InsertBefore(last, il.Create(OpCodes.Ldnull));

            il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, rpcMethod.Parameters.Count));
            il.InsertBefore(last, il.Create(OpCodes.Newarr, _systemTypeTypeReference));

            for (int i = 0; i < rpcMethod.Parameters.Count; i++) {
                var parameter = rpcMethod.Parameters[i];

                il.InsertBefore(last, il.Create(OpCodes.Dup));
                il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, (int)i));

                il.InsertBefore(last, il.Create(OpCodes.Ldtoken, parameter.ParameterType));
                il.InsertBefore(last, il.Create(OpCodes.Call, _systemTypeGetTypeFromHandleMethod));

                il.InsertBefore(last, il.Create(OpCodes.Stelem_Ref));
            }
            il.InsertBefore(last, il.Create(OpCodes.Ldnull));

            il.InsertBefore(last, il.Create(OpCodes.Callvirt, _systemTypeGetMethodMethod));
            il.InsertBefore(last, il.Create(OpCodes.Callvirt, _dictionaryAddMethod));
        }

        void InjectMethodCallAtEnd(MethodDefinition method, MethodDefinition methodCall) {
            var il = method.Body.GetILProcessor();
            var last = il.Body.Instructions[il.Body.Instructions.Count - 1];

            il.InsertBefore(last, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(last, il.Create(OpCodes.Callvirt, methodCall));
        }

        MethodDefinition CreateRpcRemoteMethod(MethodDefinition method) {
            var newMethod = new MethodDefinition(method.Name + SEND_RPC_METHOD_NAME_POSTFIX, method.Attributes, method.ReturnType);

            foreach (var param in method.Parameters)
                newMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));

            return newMethod;
        }

        bool InjectSendRpcInstructions(TypeDefinition type, byte methodId, MethodDefinition method) {
            string error;
            if (IsRpcMethodValid(method, out error)) {
                InjectValidSendRpcInstructions(methodId, method);
                //Utilities.LogWarning("RPC method patched \"" + method.FullName + "\"");
            }
            else {
                InjectInvalidRpcInstructions(method, error);
                Debug.LogError("RPC method error \"" + method.FullName + "\": " + error);
                return false;
            }

            return true;
        }

        void InjectValidSendRpcInstructions(int methodId, MethodDefinition method) {
            var il = method.Body.GetILProcessor();

            var firstInstruction = il.Create(OpCodes.Ldarg_0);
            il.Append(firstInstruction);

            //target validation
            var type = (int)GetAttributeByName("Cube.Networking.Replicas.ReplicaRpcAttribute", method.CustomAttributes).ConstructorArguments[0].Value;

            string error = "Cannot call rpc method \"" + method.FullName + "\" on client";
            Instruction conditionInstruction = il.Create(OpCodes.Call, ImportMethod(_isClientProperty.GetMethod));
            if (type == _rpcTargetServerValue) {
                error = "Cannot call rpc method \"" + method.FullName + "\" on server";
                conditionInstruction = il.Create(OpCodes.Call, ImportMethod(_isServerProperty.GetMethod));
            }

            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(firstInstruction, conditionInstruction);
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Brfalse, firstInstruction));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, error));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, _debugLogErrorMethod));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ret));

            //method id
            il.Append(il.Create(OpCodes.Ldc_I4, (int)methodId));

            //parameters
            il.Append(il.Create(OpCodes.Ldc_I4_S, (sbyte)method.Parameters.Count));
            il.Append(il.Create(OpCodes.Newarr, _objectTypeReference));

            foreach (var param in method.Parameters) {
                il.Append(il.Create(OpCodes.Dup));
                il.Append(il.Create(OpCodes.Ldc_I4_S, (sbyte)param.Index));
                il.Append(il.Create(OpCodes.Ldarg_S, (byte)(param.Index + 1)));
                il.Append(il.Create(OpCodes.Box, param.ParameterType));
                il.Append(il.Create(OpCodes.Stelem_Ref));
            }

            il.Append(il.Create(OpCodes.Call, _sendRpcMethod));
            il.Append(il.Create(OpCodes.Ret));
        }

        void InjectInvalidRpcInstructions(MethodDefinition method, string error) {
            ILProcessor ilProcessor = method.Body.GetILProcessor();

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldstr, error));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, _debugLogErrorMethod));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        bool IsRpcMethodValid(MethodDefinition method, out string error) {
            error = "";

            if (!method.Name.StartsWith("Rpc")) {
                error = "Rpc method name must start with \"Rpc\"";
                return false;
            }

            if (method.IsPublic) {
                error = "Rpc method cannot be public";
                return false;
            }

            if (method.IsVirtual) {
                error = "Rpc method cannot be virtual";
                return false;
            }

            if (method.ReturnType.FullName != "System.Void") {
                error = "Rpc method cannot return a value";
                return false;
            }

            if (method.IsStatic) {
                error = "Rpc method cannot be static";
                return false;
            }

            if (!InheritsTypeFrom(method.DeclaringType, "Cube.Networking.Replicas.ReplicaBehaviour")) {
                error = "Rpc methods are only supported in \"ReplicaBehaviour\"";
                return false;
            }

            foreach (var param in method.Parameters) {
                if (param.IsOut) {
                    error = "Rpc methods does not support out parameters";
                    return false;
                }

                if (param.ParameterType.IsByReference) {
                    error = "Rpc methods does not support ref parameters";
                    return false;
                }

                if (IsDelegate(param)) {
                    error = "Rpc methods does not support delegates as parameter";
                    return false;
                }

                if (param.ParameterType.IsFunctionPointer) {
                    error = "Rpc methods does not support function pointers as parameter";
                    return false;
                }
            }

            return true;
        }
    }
}

#endif
