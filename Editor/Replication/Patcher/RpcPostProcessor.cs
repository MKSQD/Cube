using System;
using System.Collections.Generic;
using System.Linq;
using Cube.Replication;
using Mono.Cecil;
using Mono.Cecil.Cil;

class RpcPostProcessor : PostProcessor {
    TypeDefinition replicaType;
    FieldReference replicaIdField;

    TypeReference voidTypeReference;



    MethodReference debugLogErrorMethod;

    MethodReference queueServerRpcMethod;
    FieldReference replicaComponentIdxField;
    FieldReference replicaField;

    PropertyDefinition isServerProperty, isClientProperty;
    PropertyDefinition clientProperty;
    PropertyDefinition replicaManagerProperty;

    PropertyDefinition networkInterfaceProperty;

    MethodDefinition replicaManagerGetReplicaMethod;

    TypeDefinition bitWriterType;
    TypeDefinition bitReaderType;
    MethodReference bitWriterCTorMethod;
    Dictionary<string, MethodReference> bitStreamWrite = new Dictionary<string, MethodReference>();
    Dictionary<string, MethodReference> bitStreamRead = new Dictionary<string, MethodReference>();
    MethodReference readNetworkObject;
    MethodReference readSerializable;

    MethodReference clientNetworkInterfaceSendMethod;

    HashSet<string> processedTypes = new HashSet<string>();

    public RpcPostProcessor(ModuleDefinition module)
        : base(module) {
        var unityEngineAssembly = ResolveAssembly("UnityEngine.CoreModule");
        var cubeAssembly = ResolveAssembly("Cube");

        var clientNetworkInterfaceType = GetTypeDefinitionByName(cubeAssembly, "Cube.Transport.IClientNetworkInterface");
        clientNetworkInterfaceSendMethod = Import(GetMethodDefinitionByName(clientNetworkInterfaceType, "Send"));

        {
            bitWriterType = GetTypeDefinitionByName(cubeAssembly, "Cube.Transport.BitWriter");
            bitWriterCTorMethod = Import(GetMethodDefinitionByName(bitWriterType, ".ctor"));

            foreach (var method in bitWriterType.Methods.Where(m => m.Name.StartsWith("Write"))) {
                if (method.Name.Contains("Normalised") || method.Name.Contains("Lossy") || method.Name == "WriteIntInRange")
                    continue;

                if (method.Parameters.Count == 0)
                    continue;

                bitStreamWrite[method.Parameters[0].ParameterType.Name] = Import(method);
            }
        }
        {
            bitReaderType = GetTypeDefinitionByName(cubeAssembly, "Cube.Transport.BitReader");

            foreach (var method in bitReaderType.Methods.Where(m => m.Name.StartsWith("Read"))) {
                if (method.Name.Contains("Normalised") || method.Name.Contains("Lossy") || method.Name == "ReadRaw" || method.Name == "ReadIntInRange")
                    continue;

                bitStreamRead[method.ReturnType.Name] = Import(method);
            }
            readSerializable = GetMethodDefinitionByName(bitReaderType, "ReadSerializable");
        }








        var bitStreamExtensionsType = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.BitStreamExtensions");
        foreach (var writeMethod in bitStreamExtensionsType.Methods.Where(m => m.Name.StartsWith("Write"))) {
            bitStreamWrite[writeMethod.Parameters[1].ParameterType.Name] = Import(writeMethod);
        }
        foreach (var readMethod in bitStreamExtensionsType.Methods.Where(m => m.Name.StartsWith("Read"))) {
            bitStreamRead[readMethod.ReturnType.Name] = Import(readMethod);
        }

        readNetworkObject = GetMethodDefinitionByName(bitStreamExtensionsType, "ReadNetworkObject");

        var baseReplicaBehaviourType = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.BaseReplicaBehaviour");

        var replicaBehaviourType = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.ReplicaBehaviour");
        replicaType = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.Replica");
        replicaIdField = Import(GetFieldDefinitionByName(replicaType, "Id"));

        var debugType = GetTypeDefinitionByName(unityEngineAssembly, "UnityEngine.Debug");
        debugLogErrorMethod = Import(GetMethodDefinitionByName(debugType, "LogError"));

        queueServerRpcMethod = Import(GetMethodDefinitionByName(replicaType, "QueueServerRpc"));

        replicaComponentIdxField = Import(GetFieldDefinitionByName(replicaBehaviourType, "ReplicaComponentIdx"));
        replicaField = Import(GetFieldDefinitionByName(baseReplicaBehaviourType, "Replica"));

        isServerProperty = GetPropertyDefinitionByName(baseReplicaBehaviourType, "isServer");
        isClientProperty = GetPropertyDefinitionByName(baseReplicaBehaviourType, "isClient");
        clientProperty = GetPropertyDefinitionByName(baseReplicaBehaviourType, "client");
        replicaManagerProperty = GetPropertyDefinitionByName(replicaBehaviourType, "ReplicaManager");

        var cubeClientType = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.ICubeClient");
        networkInterfaceProperty = GetPropertyDefinitionByName(cubeClientType, "NetworkInterface");

        var ireplicaManager = GetTypeDefinitionByName(cubeAssembly, "Cube.Replication.IReplicaManager");
        replicaManagerGetReplicaMethod = GetMethodDefinitionByName(ireplicaManager, "GetReplica");

        voidTypeReference = module.TypeSystem.Void;
    }

    public override bool Process(ModuleDefinition module) {
        var anythingChanged = false;

        foreach (var type in module.Types) {
            anythingChanged |= ProcessType(type, module);
            foreach (var nestedType in type.NestedTypes) {
                anythingChanged |= ProcessType(nestedType, module);
            }
        }

        return anythingChanged;
    }

    bool ProcessType(TypeDefinition type, ModuleDefinition module) => ProcessType(type, module, out byte _);

    bool ProcessType(TypeDefinition type, ModuleDefinition module, out byte numRpcs) {
        numRpcs = 0;

        if (!type.IsClass || !type.HasMethods)
            return false;

        if (processedTypes.Contains(type.FullName))
            return false;

        processedTypes.Add(type.FullName);



        var anythingChanged = false;

        byte nextRpcMethodId = 0;
        if (TypeInheritsFrom(type, "Cube.Replication.ReplicaBehaviour")) {
            var baseType = ResolveTypeReference(type.BaseType);

            // Process base type first
            // Detail: technically, we should only process types in our module (baseType.Module.Name == module.Name),
            //         but if our base type is in another module we won't be able to call base.DispatchRpc because
            //         assemblies are only resolved to their non-patched versions
            if (baseType.FullName != "Cube.Replication.ReplicaBehaviour") {
                anythingChanged |= ProcessType(baseType, module, out byte numRpcsBaseType);
                nextRpcMethodId = numRpcsBaseType;
            }
        }

        var remoteMethods = new List<(byte, MethodDefinition)>();
        foreach (var method in type.Methods) {
            if (method.IsConstructor || !method.HasBody)
                continue;

            var isReplicaRpc = method.HasCustomAttributes && HasAttribute("Cube.Replication.ReplicaRpcAttribute", method);
            if (!isReplicaRpc)
                continue;

            if (!IsRpcMethodValid(method, out string error))
                throw new Exception($"RPC method error \"{method.FullName}\": {error}");

            var implMethod = new MethodDefinition($"RpcImpl_{method.Name}", method.Attributes, method.ReturnType);
            foreach (var param in method.Parameters) {
                implMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            CopyMethodBody(method, implMethod);
            method.Body.Instructions.Clear();
            InjectSendRpcInstructions(nextRpcMethodId, method);
            remoteMethods.Add((nextRpcMethodId, implMethod));

            nextRpcMethodId++;
            if (nextRpcMethodId == byte.MaxValue)
                throw new Exception($"Reached max RPC method count {nextRpcMethodId} for type {type.FullName}!");
        }

        if (remoteMethods.Count == 0)
            return anythingChanged;

        var dispatchRpcs = CreateDispatchRpcs(remoteMethods, ResolveTypeReference(type.BaseType));
        type.Methods.Add(dispatchRpcs);

        foreach (var method in remoteMethods) {
            type.Methods.Add(method.Item2);
        }

        numRpcs = (byte)remoteMethods.Count;
        return true;
    }

    MethodDefinition CreateDispatchRpcs(List<(byte, MethodDefinition)> remoteMethods, TypeDefinition baseType) {
        var method = new MethodDefinition("DispatchRpc", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, voidTypeReference);
        method.Parameters.Add(new ParameterDefinition("methodIdx", ParameterAttributes.None, MainModule.TypeSystem.Byte));
        method.Parameters.Add(new ParameterDefinition("bs", ParameterAttributes.None, Import(bitReaderType)));

        method.Body.InitLocals = true;

        var il = method.Body.GetILProcessor();

        var nopJumpTargets = new List<Instruction>();

        // switch (methodIdx)
        for (int i = 0; i < remoteMethods.Count; ++i) {
            var boo = il.Create(OpCodes.Nop);
            nopJumpTargets.Add(boo);

            var methodIdx = remoteMethods[i].Item1;
            il.Emit(OpCodes.Ldarg_1);
            if (methodIdx == 0) {
                il.Emit(OpCodes.Brfalse_S, boo);
            } else {
                il.Emit(OpCodes.Ldc_I4_S, (sbyte)methodIdx);
                il.Emit(OpCodes.Beq_S, boo);
            }
        }

        var boo2 = il.Create(OpCodes.Nop);
        nopJumpTargets.Add(boo2);
        il.Emit(OpCodes.Br_S, boo2);

        // T argN = bs.Read...();
        for (int i = 0; i < remoteMethods.Count; ++i) {
            var remoteMethod = remoteMethods[i].Item2;

            il.Append(nopJumpTargets[i]);

            var moo = new List<byte>();
            for (int j = 0; j < remoteMethod.Parameters.Count; ++j) {
                var param = remoteMethod.Parameters[j];
                var typeDef = param.ParameterType.Resolve();

                MethodReference result = null;
                bool isReplica = param.ParameterType.Name == "Replica";
                bool isNetworkObject = TypeInheritsFrom(typeDef, "Cube.Replication.NetworkObject");
                bool isSerializable = typeDef.Interfaces.Any(type => type.InterfaceType.FullName == typeof(Cube.Transport.IBitSerializable).FullName);
                if (isReplica) {
                    result = bitStreamRead["ReplicaId"];
                } else if (isNetworkObject) {
                    result = bitStreamRead["T"];
                } else if (isSerializable) {
                    result = readSerializable;
                } else {
                    if (typeDef.IsEnum) {
                        typeDef = GetEnumUnderlyingType(typeDef).Resolve();
                    }

                    result = bitStreamRead[typeDef.Name];
                }

                moo.Add((byte)(method.Body.Variables.Count));

                if (isReplica) {
                    method.Body.Variables.Add(new VariableDefinition(Import(replicaType)));

                    // Replica replica = base.ReplicaManager.GetReplica(bs.ReadReplicaId());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, Import(replicaManagerProperty.GetMethod));

                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Call, Import(result.Resolve()));
                    il.Emit(OpCodes.Callvirt, Import(replicaManagerGetReplicaMethod));

                    il.Emit(OpCodes.Stloc, method.Body.Variables.Count - 1);
                } else if (isNetworkObject) {
                    method.Body.Variables.Add(new VariableDefinition(Import(param.ParameterType.Resolve())));

                    // Replace generic arguments with some vodoo magic
                    var mmm = new GenericInstanceMethod(readNetworkObject);
                    mmm.GenericArguments.Add(Import(param.ParameterType.Resolve()));

                    var mr = MainModule.ImportReference(mmm);

                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Call, mr);

                    il.Emit(OpCodes.Stloc, method.Body.Variables.Count - 1);
                } else if (isSerializable) {
                    method.Body.Variables.Add(new VariableDefinition(Import(param.ParameterType.Resolve())));

                    // default(DamageInfo).Deserialize(bs);
                    il.Emit(OpCodes.Ldloca, method.Body.Variables.Count - 1);
                    il.Emit(OpCodes.Initobj, param.ParameterType.Resolve());
                    il.Emit(OpCodes.Ldloca, method.Body.Variables.Count - 1);
                    il.Emit(OpCodes.Ldarg_2);

                    var deserialize = Import(GetMethodDefinitionByName(param.ParameterType.Resolve(), "Deserialize"));
                    il.Emit(OpCodes.Call, deserialize);
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

        // ((ReplicaBehaviour)this).DispatchRpc(methodIdx, bs);
        il.Append(nopJumpTargets[^1]);

        if (baseType != null) {
            var md = GetMethodDefinitionByName(baseType, "DispatchRpc");
            if (md != null) {
                var dispatchRpcMethod = Import(md);


                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, dispatchRpcMethod);
            }
        }

        il.Emit(OpCodes.Ret);

        return method;
    }

    void InjectSendRpcInstructions(int methodId, MethodDefinition method) {
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Variables.Add(new VariableDefinition(Import(bitWriterType)));

        var rpcTarget = (RpcTarget)GetAttributeByName("Cube.Replication.ReplicaRpcAttribute", method.CustomAttributes).ConstructorArguments[0].Value;

        // target validation
        string error;
        MethodDefinition serverOrClientGetMethod;
        if (rpcTarget == RpcTarget.Server) {
            error = $"Cannot call RPC method '{method.FullName}' on server";
            serverOrClientGetMethod = isClientProperty.GetMethod;
        } else {
            error = $"Cannot call RPC method '{method.FullName}' on client";
            serverOrClientGetMethod = isServerProperty.GetMethod;
        }

        var il = method.Body.GetILProcessor();

        // Check isClient/isServer
        var ok = il.Create(OpCodes.Nop);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, Import(serverOrClientGetMethod));
        il.Emit(OpCodes.Brtrue_S, ok);
        il.Emit(OpCodes.Ldstr, error);
        il.Emit(OpCodes.Call, debugLogErrorMethod);
        il.Emit(OpCodes.Ret);
        il.Append(ok);


        // BitWriter bitStream = new BitWriter();
        il.Emit(OpCodes.Ldc_I4, 32);
        il.Emit(OpCodes.Newobj, bitWriterCTorMethod);
        il.Emit(OpCodes.Stloc_0);


        // bitStream.Write((byte)Cube.Transport.MessageId.ReplicaRpc);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldc_I4, (int)Cube.Transport.MessageId.ReplicaRpc);
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

            TypeDefinition typeDef = param.ParameterType.Resolve();
            bool isReplica = param.ParameterType.Name == "Replica";
            bool isNetworkObject = TypeInheritsFrom(typeDef, "Cube.Replication.NetworkObject");
            bool isSerializable = typeDef.Interfaces.Any(type => type.InterfaceType.FullName == typeof(Cube.Transport.IBitSerializable).FullName);
            if (isReplica) {
                result = bitStreamWrite["ReplicaId"];
            } else if (isNetworkObject) {
                result = bitStreamWrite["NetworkObject"];
            } else if (isSerializable) {
                // #todo slow: bs.Write((ISerializable)...) should be: arg.Serialize(bs);
                result = bitStreamWrite["IBitSerializable"];
            } else {
                if (typeDef.IsEnum) {
                    typeDef = GetEnumUnderlyingType(typeDef).Resolve();
                }

                try {
                    result = bitStreamWrite[typeDef.Name];
                } catch (KeyNotFoundException) {
                    throw new Exception($"Rpc argument of type {typeDef.Name} not supported ({method})");
                }
            }

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg, i + 1);
            if (isReplica) {
                il.Emit(OpCodes.Ldfld, replicaIdField);
            } else if (isSerializable) {
                il.Emit(OpCodes.Box, typeDef);
            }
            il.Emit(OpCodes.Call, result);

        }

        if (rpcTarget == RpcTarget.Server) {
            // base.client.networkInterface.Send(bitStream, PacketReliability.Unreliable);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, Import(clientProperty.GetMethod));
            il.Emit(OpCodes.Callvirt, Import(networkInterfaceProperty.GetMethod));

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldc_I4_0); // PacketReliability.Unreliable
            il.Emit(OpCodes.Ldc_I4_0); // sequenceChannel
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
