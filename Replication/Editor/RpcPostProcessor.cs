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

        TypeDefinition _replicaBehaviourType;
        TypeDefinition replicaType;
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
        PropertyDefinition replicaManagerProperty;

        PropertyDefinition networkInterfaceProperty;

        MethodDefinition replicaManagerGetReplicaMethod;

        TypeDefinition bitStreamType;
        MethodReference bitStreamCTorMethod;
        Dictionary<string, MethodReference> bitStreamWrite = new Dictionary<string, MethodReference>();
        Dictionary<string, MethodReference> bitStreamRead = new Dictionary<string, MethodReference>();

        MethodReference clientNetworkInterfaceSendMethod;

        HashSet<string> processedTypes = new HashSet<string>();

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
            foreach (var readMethod in bitStreamType.Methods.Where(m => m.Name.StartsWith("Read"))) {
                if (readMethod.Name.Contains("Normalised") || readMethod.Name.Contains("Lossy"))
                    continue;

                bitStreamRead[readMethod.ReturnType.Name] = readMethod;
            }

            var bitStreamExtensionsType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.BitStreamExtensions");
            foreach (var writeMethod in Import(GetMethodDefinitionsByName(bitStreamExtensionsType, "Write"))) {
                bitStreamWrite[writeMethod.Parameters[1].ParameterType.Name] = writeMethod;
            }
            foreach (var readMethod in bitStreamExtensionsType.Methods.Where(m => m.Name.StartsWith("Read"))) {
                bitStreamRead[readMethod.ReturnType.Name] = readMethod;
            }

            _replicaBehaviourType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.ReplicaBehaviour");
            replicaType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.Replica");
            replicaIdField = Import(GetFieldDefinitionByName(replicaType, "Id"));

            var debugType = GetTypeDefinitionByName(unityEngineAssembly, "UnityEngine.Debug");
            _debugLogErrorMethod = Import(GetMethodDefinitionByName(debugType, "LogError"));

            queueServerRpcMethod = Import(GetMethodDefinitionByName(replicaType, "QueueServerRpc"));

            replicaComponentIdxField = Import(GetFieldDefinitionByName(_replicaBehaviourType, "replicaComponentIdx"));
            replicaField = Import(GetFieldDefinitionByName(_replicaBehaviourType, "Replica"));

            _isServerProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "isServer");
            _isClientProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "isClient");
            clientProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "client");
            replicaManagerProperty = GetPropertyDefinitionByName(_replicaBehaviourType, "ReplicaManager");

            var cubeClientType = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.ICubeClient");
            networkInterfaceProperty = GetPropertyDefinitionByName(cubeClientType, "networkInterface");

            var ireplicaManager = GetTypeDefinitionByName(replicationAssembly, "Cube.Replication.IReplicaManager");
            replicaManagerGetReplicaMethod = GetMethodDefinitionByName(ireplicaManager, "GetReplica");



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
        }

        public override void Process(ModuleDefinition module) {
            foreach (var type in module.Types) {
                try {
                    ProcessType(type, module);
                } catch (Exception e) {
                    Debug.LogException(e);
                }

                foreach (var nestedType in type.NestedTypes) {
                    try {
                        ProcessType(nestedType, module);
                    } catch (Exception e) {
                        Debug.LogException(e);
                    }
                }
            }
        }

        void ProcessType(TypeDefinition type, ModuleDefinition module) {
            if (!type.IsClass || !type.HasMethods)
                return;

            if (processedTypes.Contains(type.FullName))
                return;

            processedTypes.Add(type.FullName);

            byte nextRpcMethodId = 0;
            if (TypeInheritsFrom(type, "Cube.Replication.ReplicaBehaviour")) {
                var baseType = ResolveTypeReference(type.BaseType);

                if (baseType.FullName != "Cube.Replication.ReplicaBehaviour" && baseType.Module.Name == module.Name) {
                    ProcessType(baseType, module);
                }
            }

            var remoteMethods = new List<MethodDefinition>();
            var rpcMethods = new Dictionary<byte, MethodDefinition>();
            foreach (var method in type.Methods) {
                if (method.IsConstructor || !method.HasBody)
                    continue;
                if (!method.HasCustomAttributes || !HasAttribute("Cube.Replication.ReplicaRpcAttribute", method))
                    continue;

                var implMethod = new MethodDefinition(method.Name + RPC_IMPL, method.Attributes, method.ReturnType);
                foreach (var param in method.Parameters) {
                    implMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
                }

                CopyMethodBody(method, implMethod);
                ClearMethodBody(method);

                if (IsRpcMethodValid(method, out string error)) {
                    InjectSendRpcInstructions(nextRpcMethodId, method, implMethod);
                } else {
                    Debug.LogError("RPC method error \"" + method.FullName + "\": " + error);
                }

                remoteMethods.Add(implMethod);
                rpcMethods.Add(nextRpcMethodId, method);

                nextRpcMethodId++;
                if (nextRpcMethodId == byte.MaxValue) {
                    Debug.LogError($"Reached max RPC method count {nextRpcMethodId} for type {type.FullName}!");
                    break;
                }
            }

            if (remoteMethods.Count > 0) {
                var dispatchRpcs = CreateDispatchRpcs(remoteMethods);
                type.Methods.Add(dispatchRpcs);

                foreach (var method in remoteMethods) {
                    type.Methods.Add(method);
                }
            }
        }

        MethodDefinition CreateDispatchRpcs(List<MethodDefinition> remoteMethods) {
            var method = new MethodDefinition("DispatchRpc", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Virtual, _voidTypeReference);
            method.Parameters.Add(new ParameterDefinition("methodIdx", Mono.Cecil.ParameterAttributes.None, mainModule.TypeSystem.Byte));
            method.Parameters.Add(new ParameterDefinition("bs", Mono.Cecil.ParameterAttributes.None, Import(bitStreamType)));

            method.Body.InitLocals = true;

            var il = method.Body.GetILProcessor();

            var foo = new List<Instruction>();

            // switch (methodIdx)
            for (int i = 0; i < remoteMethods.Count; ++i) {
                var boo = il.Create(OpCodes.Nop);
                foo.Add(boo);

                il.Emit(OpCodes.Ldarg_1);
                if (i == 0) {
                    il.Emit(OpCodes.Brfalse_S, boo);
                } else {
                    il.Emit(OpCodes.Ldc_I4_S, (sbyte)i);
                    il.Emit(OpCodes.Beq_S, boo);
                }
            }

            var boo2 = il.Create(OpCodes.Nop);
            foo.Add(boo2);
            il.Emit(OpCodes.Br_S, boo2);

            // T argN = bs.Read...();
            for (int i = 0; i < remoteMethods.Count; ++i) {
                var remoteMethod = remoteMethods[i];

                var moo = new List<byte>();
                for (int j = 0; j < remoteMethod.Parameters.Count; ++j) {
                    var param = remoteMethod.Parameters[j];
                    var typeDef = param.ParameterType.Resolve();

                    MethodReference result = null;
                    var isReplica = param.ParameterType.Name == "Replica";
                    var isNetworkObject = TypeInheritsFrom(typeDef, "Cube.Replication.NetworkObject");
                    if (isReplica) {
                        result = bitStreamRead["ReplicaId"];
                    } else if (isNetworkObject) {
                        result = bitStreamRead["T"];
                    } else {
                        if (typeDef.IsEnum) {
                            typeDef = GetEnumUnderlyingType(typeDef).Resolve();
                        }

                        try {
                            result = bitStreamRead[typeDef.Name];
                        } catch (KeyNotFoundException) {
                            Debug.LogError($"Rpc argument of type {typeDef.Name} not supported ({remoteMethod})");
                            //throw;
                        }
                    }

                    if (j == 0) {
                        il.Append(foo[i]);
                    }

                    moo.Add((byte)(method.Body.Variables.Count));

                    if (isReplica) {
                        method.Body.Variables.Add(new VariableDefinition(Import(replicaType)));

                        // Replica replica = base.ReplicaManager.GetReplica(bs.ReadReplicaId());
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, Import(replicaManagerProperty.GetMethod));

                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Callvirt, Import(result.Resolve()));
                        il.Emit(OpCodes.Callvirt, Import(replicaManagerGetReplicaMethod));

                        il.Emit(OpCodes.Stloc, method.Body.Variables.Count - 1);
                    } else if (isNetworkObject) {
                        method.Body.Variables.Add(new VariableDefinition(Import(param.ParameterType.Resolve())));

                        //                         var nonGenericCall = new GenericInstanceMethod(result);
                        //                         nonGenericCall.GenericArguments.Add(Import(typeDef));



                        var genInst_1_ReadNetworkObject26 = new GenericInstanceMethod(result);
                        genInst_1_ReadNetworkObject26.GenericArguments.Add(param.ParameterType);
                        genInst_1_ReadNetworkObject26.ReturnType = mainModule.ImportReference(typeDef);
                        //var Callvirt27 = il_Test_Main_.Create(OpCodes.Callvirt, genInst_1_ReadNetworkObject26);

                        // var valN = bs.ReadNetworkObject<T>();
                        // IL_0007: call !!0 [Cube.Replication]Cube.Replication.BitStreamExtensions::ReadNetworkObject<class EquippableItemType>(class [Cube.Transport]Cube.Transport.BitStream)

                        MethodInfo openGenericMethod = typeof(BitStreamExtensions).GetMethod("ReadNetworkObject");
                        MethodInfo closedGenericMethod = openGenericMethod.MakeGenericMethod(typeof(NetworkObject));
                        MethodReference mr = mainModule.ImportReference(closedGenericMethod);

                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, mr);

                        il.Emit(OpCodes.Stloc, method.Body.Variables.Count - 1);
                    } else {
                        method.Body.Variables.Add(new VariableDefinition(Import(param.ParameterType.Resolve())));

                        // var valN = bs.Read...();
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Callvirt, Import(result.Resolve()));

                        il.Emit(OpCodes.Stloc, method.Body.Variables.Count - 1);
                    }
                }

                // Rpc(arg0, arg1, ...);
                il.Emit(OpCodes.Ldarg_0);
                for (int j = 0; j < remoteMethod.Parameters.Count; ++j) {
                    var idx = moo.Count - remoteMethod.Parameters.Count + j;
                    il.Emit(OpCodes.Ldloc_S, moo[idx]);
                }
                il.Emit(OpCodes.Call, remoteMethod);
                il.Emit(OpCodes.Ret);
            }

            // Debug.LogError((object)"Missing RPC dispatch");
            il.Append(foo[foo.Count - 1]);
            il.Emit(OpCodes.Ldstr, "Missing RPC dispatch");
            il.Emit(OpCodes.Call, _debugLogErrorMethod);
            il.Emit(OpCodes.Ret);

            return method;
        }

        void InjectSendRpcInstructions(int methodId, MethodDefinition method, MethodDefinition implMethod) {
            method.Body.Variables.Clear();
            method.Body.Variables.Add(new VariableDefinition(Import(bitStreamType)));

            var rpcTarget = (RpcTarget)GetAttributeByName("Cube.Replication.ReplicaRpcAttribute", method.CustomAttributes).ConstructorArguments[0].Value;

            // target validation
            string error;
            MethodDefinition serverOrClientGetMethod;
            if (rpcTarget == RpcTarget.Server) {
                error = "Cannot call RPC method \"" + method.FullName + "\" on server";
                serverOrClientGetMethod = _isClientProperty.GetMethod;
            } else {
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
                } else if (TypeInheritsFrom(typeDef, "Cube.Replication.NetworkObject")) {
                    result = bitStreamWrite["NetworkObject"];
                } else {
                    if (typeDef.IsEnum) {
                        typeDef = GetEnumUnderlyingType(typeDef).Resolve();
                    }

                    try {
                        result = bitStreamWrite[typeDef.Name];
                    } catch (KeyNotFoundException) {
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
            } else {
                // Replica.QueueServerRpc(bitStream, RpcTarget.Owner);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, replicaField);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, (int)rpcTarget);
                il.Emit(OpCodes.Callvirt, queueServerRpcMethod);
            }

            il.Emit(OpCodes.Ret);
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

        static TypeReference GetEnumUnderlyingType(TypeDefinition self) {
            var fields = self.Fields;
            for (int i = 0; i < fields.Count; i++) {
                FieldDefinition fieldDefinition = fields[i];
                if (!fieldDefinition.IsStatic) {
                    return fieldDefinition.FieldType;
                }
            }
            throw new ArgumentException();
        }
    }
}

#endif
