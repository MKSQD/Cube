#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Cube.Replication.Editor {
    class RpcPostProcessor : PostProcessor {
        const string RPC_IMPL = "_RpcImpl";
        const string ADD_RPC_TO_RPC_MAP_METHOD_NAME = "__ADD_RPC_TO_RPC_MAP__";

        TypeDefinition _replicaBehaviourType;
        TypeDefinition _replicaType;
        FieldReference replicaIdField;

        TypeReference _voidTypeReference;
        TypeReference _objectTypeReference;
        TypeReference _systemTypeTypeReference;

        MethodReference _systemTypeGetTypeFromHandleMethod;
        MethodReference _systemTypeGetMethodMethod;

        MethodReference _debugLogErrorMethod;
        MethodReference _dictionaryAddMethod;

        MethodReference queueServerRpcMethod;
        FieldReference replicaComponentIdxField;
        FieldReference replicaField;

        PropertyDefinition _isServerProperty;
        PropertyDefinition _isClientProperty;
        PropertyDefinition clientProperty;

        PropertyDefinition networkInterfaceProperty;

        FieldReference _replicaBehaviourRpcMap;

        TypeDefinition _rpcTargetType;

        TypeDefinition bitStreamType;
        MethodReference bitStreamCTorMethod;
        Dictionary<string, MethodReference> bitStreamWrite = new Dictionary<string, MethodReference>();
        Dictionary<string, MethodReference> bitStreamRead = new Dictionary<string, MethodReference>();

        MethodReference clientNetworkInterfaceSendMethod;

        Dictionary<string, byte> _processedTypes = new Dictionary<string, byte>();

        public RpcPostProcessor(ApplicationType app, ModuleDefinition module)
            : base(app, module) {
            var unityEngineAssembly = ResolveAssembly("UnityEngine.CoreModule");
            var replicationAssembly = ResolveAssembly("Cube.Replication");
            var transportAssembly = ResolveAssembly("Cube.Transport");

            var clientNetworkInterfaceType = GetTypeDefinitionByName(transportAssembly, "Cube.Transport.IClientNetworkInterface");
            clientNetworkInterfaceSendMethod = Import(GetMethodDefinitionByName(clientNetworkInterfaceType, "Send"));

            bitStreamType = GetTypeDefinitionByName(transportAssembly, "Cube.Transport.BitStream");
            bitStreamCTorMethod = Import(GetMethodDefinitionByName(bitStreamType, ".ctor"));

            foreach (var writeMethod in Import(GetMethodDefinitionsByName(bitStreamType, "Write"))) {
                bitStreamWrite[writeMethod.Parameters[0].ParameterType.Name] = writeMethod;
            }
            foreach (var readMethod in Import(GetMethodDefinitionsByName(bitStreamType, "Read"))) {
                bitStreamRead[readMethod.Parameters[0].ParameterType.Name] = readMethod;
            }

            var bitStreamExtensionsType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.BitStreamExtensions");
            foreach (var writeMethod in Import(GetMethodDefinitionsByName(bitStreamExtensionsType, "Write"))) {
                bitStreamWrite[writeMethod.Parameters[1].ParameterType.Name] = writeMethod;
            }
            foreach (var readMethod in Import(GetMethodDefinitionsByName(bitStreamExtensionsType, "Read"))) {
                bitStreamRead[readMethod.Parameters[1].ParameterType.Name] = readMethod;
            }

            _replicaBehaviourType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.ReplicaBehaviour");
            _replicaType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.Replica");
            replicaIdField = Import(GetFieldDefinitionByName(_replicaType, "Id"));

            var debugType = GetTypeDefinitionByName(unityEngineAssembly, "UnityEngine.Debug");
            _debugLogErrorMethod = Import(GetMethodDefinitionByName(debugType, "LogError"));

            queueServerRpcMethod = Import(GetMethodDefinitionByName(_replicaType, "QueueServerRpc"));

            replicaComponentIdxField = Import(GetFieldDefinitionByName(_replicaBehaviourType, "replicaComponentIdx"));
            replicaField = Import(GetFieldDefinitionByName(_replicaBehaviourType, "Replica"));

            _isServerProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "isServer");
            _isClientProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "isClient");
            clientProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "client");

            var cubeClientType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.ICubeClient");
            networkInterfaceProperty = GetPropertyDefinitionByName(cubeClientType, "networkInterface");

            _replicaBehaviourRpcMap = Import(GetFieldDefinitionByName(_replicaBehaviourType, "_rpcMethods"));

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

            _rpcTargetType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.RpcTarget");
        }

        public override void Process(ModuleDefinition module) {
            foreach (var type in module.Types) {
                try {
                    ProcessType(type, module);
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }

                foreach (var nestedType in type.NestedTypes) {
                    try {
                        ProcessType(nestedType, module);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
                }
            }
        }

        void ProcessType(TypeDefinition type, ModuleDefinition module) {
            if (!type.IsClass || !type.HasMethods)
                return;

            if (_processedTypes.ContainsKey(type.FullName))
                return;

            byte nextRpcMethodId = 0;
            if (TypeInheritsFrom(type, "Cube.Replication.ReplicaBehaviour")) {
                var baseType = ResolveTypeReference(type.BaseType);

                // #TODO classes from other modules (filter "tmp.Module.path" in "Application.dataPath" ?)
                if (baseType.FullName != "Cube.Replication.ReplicaBehaviour" && baseType.Module.Name == module.Name) {
                    ProcessType(baseType, module);
                    nextRpcMethodId = _processedTypes[baseType.FullName];
                }
            }

            var remoteMethods = new List<MethodDefinition>();
            var rpcMethods = new Dictionary<byte, MethodDefinition>();
            foreach (var method in type.Methods) {
                if (method.IsConstructor || !method.HasBody)
                    continue;

                if (!method.HasCustomAttributes || !HasAttribute("Cube.Replication.ReplicaRpcAttribute", method))
                    continue;

                if (nextRpcMethodId == byte.MaxValue) {
                    Debug.LogError($"Reached max RPC method count {nextRpcMethodId} for type {type.FullName}!");
                    break;
                }

                var implMethod = CreateRpcRemoteMethod(method);
                remoteMethods.Add(implMethod);

                CopyMethodBody(method, implMethod);
                ClearMethodBody(method);

                if (InjectSendRpcInstructions(nextRpcMethodId, method, implMethod)) {
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

                foreach (var method in remoteMethods) {
                    type.Methods.Add(method);
                }
            }

            _processedTypes.Add(type.FullName, nextRpcMethodId);
        }

        MethodDefinition CreateAddRpcsToMapMethod(TypeDefinition type) {
            var addRpcsToMapMethod = GetMethodDefinitionByName(type, ADD_RPC_TO_RPC_MAP_METHOD_NAME);

            if (addRpcsToMapMethod != null) {
                ClearMethodBody(addRpcsToMapMethod);
            }
            else {
                addRpcsToMapMethod = new MethodDefinition(ADD_RPC_TO_RPC_MAP_METHOD_NAME, Mono.Cecil.MethodAttributes.Private, _voidTypeReference);
            }

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
            il.InsertBefore(last, il.Create(OpCodes.Ldstr, rpcMethod.Name + RPC_IMPL));
            il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, (int)(BindingFlags.NonPublic | BindingFlags.Instance)));
            il.InsertBefore(last, il.Create(OpCodes.Ldnull));

            il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, rpcMethod.Parameters.Count));
            il.InsertBefore(last, il.Create(OpCodes.Newarr, _systemTypeTypeReference));

            for (int i = 0; i < rpcMethod.Parameters.Count; i++) {
                var parameter = rpcMethod.Parameters[i];

                il.InsertBefore(last, il.Create(OpCodes.Dup));
                il.InsertBefore(last, il.Create(OpCodes.Ldc_I4, i));

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
            var newMethod = new MethodDefinition(method.Name + RPC_IMPL, method.Attributes, method.ReturnType);

            foreach (var param in method.Parameters) {
                newMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            return newMethod;
        }

        bool InjectSendRpcInstructions(byte methodId, MethodDefinition method, MethodDefinition implMethod) {
            if (!IsRpcMethodValid(method, out string error)) {
                InjectInvalidRpcInstructions(method, error);
                Debug.LogError("RPC method error \"" + method.FullName + "\": " + error);
                return false;
            }
            InjectValidSendRpcInstructions(methodId, method, implMethod);
            //Utilities.LogWarning("RPC method patched \"" + method.FullName + "\"");
            return true;
        }

        void InjectValidSendRpcInstructions(int methodId, MethodDefinition method, MethodDefinition implMethod) {
            method.Body.Variables.Clear();
            method.Body.Variables.Add(new VariableDefinition(Import(bitStreamType)));

            var rpcTarget = (RpcTarget)GetAttributeByName("Cube.Replication.ReplicaRpcAttribute", method.CustomAttributes).ConstructorArguments[0].Value;

            // target validation
            string error;
            MethodDefinition serverOrClientGetMethod;
            if (rpcTarget == RpcTarget.Server) {
                error = "Cannot call RPC method \"" + method.FullName + "\" on server";
                serverOrClientGetMethod = _isClientProperty.GetMethod;
            }
            else {
                error = "Cannot call RPC method \"" + method.FullName + "\" on client";
                serverOrClientGetMethod = _isServerProperty.GetMethod;
            }

            var il = method.Body.GetILProcessor();

            // Check isClient/isServer
            var ok = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, Import(serverOrClientGetMethod));
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse_S, ok);
            il.Emit(OpCodes.Ldstr, error);
            il.Emit(OpCodes.Call, _debugLogErrorMethod);
            il.Emit(OpCodes.Ret);
            il.Append(ok);


            // BitStream bitStream = new BitStream();
            il.Emit(OpCodes.Ldc_I4, 64);
            il.Emit(OpCodes.Newobj, bitStreamCTorMethod);
            il.Emit(OpCodes.Stloc_0);


            // bitStream.Write((byte)Cube.Transport.MessageId.ReplicaRpc);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldc_I4, 1);
            il.Emit(OpCodes.Callvirt, bitStreamWrite["Byte"]);

            // BitStreamExtensions.Write(bitStream, Replica.Id);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, replicaField);
            il.Emit(OpCodes.Ldfld, replicaIdField);
            il.Emit(OpCodes.Call, bitStreamWrite["ReplicaId"]);

            // bitStream.Write(replicaComponentIdx);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, replicaComponentIdxField);
            il.Emit(OpCodes.Callvirt, bitStreamWrite["Byte"]);

            // bitStream.Write((byte)methodId);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldc_I4, methodId);
            il.Emit(OpCodes.Callvirt, bitStreamWrite["Byte"]);

            // bitStream.Write(...);
            for (int i = 0; i < method.Parameters.Count; ++i) { // 0 == this
                var param = method.Parameters[i];

                MethodReference result;

                var typeDef = param.ParameterType.Resolve();

                var isReplica = param.ParameterType.Name == "Replica";
                if (isReplica) {
                    result = bitStreamWrite["ReplicaId"];
                }
                else if (TypeInheritsFrom(typeDef, "Cube.Replication.NetworkObject")) {
                    result = bitStreamWrite["NetworkObject"];
                }
                else {
                    if (typeDef.IsEnum) {
                        typeDef = GetEnumUnderlyingType(typeDef).Resolve();
                    }

                    try {
                        result = bitStreamWrite[typeDef.Name];
                    }
                    catch (KeyNotFoundException) {
                        Debug.LogError($"Rpc argument of type {typeDef.Name} not supported ({method})");
                        return;
                    }
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (isReplica) {
                    il.Emit(OpCodes.Ldfld, replicaIdField);
                }
                il.Emit(OpCodes.Callvirt, result);
            }

            if (rpcTarget == RpcTarget.Server) {
                // base.client.networkInterface.Send(bitStream, PacketPriority.Immediate, PacketReliability.Unreliable);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Import(clientProperty.GetMethod));
                il.Emit(OpCodes.Callvirt, Import(networkInterfaceProperty.GetMethod));

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, clientNetworkInterfaceSendMethod);
            }
            else {
                // Replica.QueueServerRpc(bitStream, RpcTarget.Owner);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, replicaField);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, (int)rpcTarget);
                il.Emit(OpCodes.Callvirt, queueServerRpcMethod);
            }

            il.Emit(OpCodes.Ret);
        }

        public static TypeReference GetEnumUnderlyingType(TypeDefinition self) {
            var fields = self.Fields;
            for (int i = 0; i < fields.Count; i++) {
                FieldDefinition fieldDefinition = fields[i];
                if (!fieldDefinition.IsStatic) {
                    return fieldDefinition.FieldType;
                }
            }
            throw new ArgumentException();
        }

        void InjectInvalidRpcInstructions(MethodDefinition method, string error) {
            ILProcessor ilProcessor = method.Body.GetILProcessor();

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldstr, error));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, _debugLogErrorMethod));

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        bool IsRpcMethodValid(MethodDefinition method, out string error) {
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
            if (!TypeInheritsFrom(method.DeclaringType, "Cube.Replication.ReplicaBehaviour")) {
                error = "Rpc methods are only supported in \"ReplicaBehaviour\"";
                return false;
            }

            foreach (var param in method.Parameters) {
                if (param.IsOut || param.ParameterType.IsByReference) {
                    error = "Rpc methods does not support out or ref parameters";
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

            error = "";
            return true;
        }
    }
}

#endif
