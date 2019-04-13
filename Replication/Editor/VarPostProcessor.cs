#if UNITY_EDITOR

using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Cube.Replication.Editor {
    class VarPostProcessor : PostProcessor {
        TypeDefinition _replicaBehaviourType;
        TypeReference _replicaSerializationModeType;
        TypeReference _replicaViewType;
        TypeReference _replicaVarAttributeType;
        FieldReference _replicaDirtyFieldsMask;

        TypeDefinition _bitStreamType;
        Dictionary<string, MethodReference> _bitStreamWrite = new Dictionary<string, MethodReference>();
        Dictionary<string, MethodReference> _bitStreamRead = new Dictionary<string, MethodReference>();
        MethodReference _bitStreamWriteBool;
        MethodReference _bitStreamReadBool;

        Dictionary<FieldDefinition, MethodDefinition> _setters = new Dictionary<FieldDefinition, MethodDefinition>();

        public VarPostProcessor(ApplicationType appType, ModuleDefinition module) : base(appType, module) {
            var networkingReplicaAssembly = ResolveAssembly("Cube.Replication");
            var networkingTransportAssembly = ResolveAssembly("Cube.Transport");

            _replicaBehaviourType = GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Replication.ReplicaBehaviour");
            _replicaDirtyFieldsMask = ImportField(GetFieldDefinitionByName(_replicaBehaviourType, "dirtyFieldsMask"));

            _replicaSerializationModeType = ImportType(GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Replication.ReplicaSerializationMode"));
            _replicaVarAttributeType = ImportType(GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Replication.ReplicaVarAttribute"));

            _bitStreamType = GetTypeDefinitionByName(networkingTransportAssembly.MainModule, "Cube.Transport.BitStream");

            foreach (var writeMethod in ImportMethods(GetMethodDefinitionsByName(_bitStreamType, "Write")))
                _bitStreamWrite[writeMethod.Parameters[0].ParameterType.Name] = writeMethod;
            foreach (var readMethod in ImportMethods(GetMethodDefinitionsByName(_bitStreamType, "Read")))
                _bitStreamRead[readMethod.Parameters[0].ParameterType.Name] = readMethod;

            _bitStreamWriteBool = _bitStreamWrite[module.TypeSystem.Boolean.Name];
            _bitStreamReadBool = ImportMethod(GetMethodDefinitionByName(_bitStreamType, "ReadBool"));

            if ((appType & ApplicationType.Server) != 0) {
                _replicaViewType = ImportType(GetTypeDefinitionByName(networkingReplicaAssembly.MainModule, "Cube.Replication.ReplicaView"));
            }
        }

        public override bool Process() {
            foreach (var type in module.Types) {
                ProcessType(type);
            }

            foreach (var type in module.Types)
                ProcessCallsites(type);

            return true;
        }

        void ProcessType(TypeDefinition type) {
            if (!type.IsClass)
                return;

            foreach (var nestedType in type.NestedTypes) {
                ProcessType(nestedType);
            }

            var fields = FindReplicaVarFields(type);
            if (fields.Count == 0)
                return;

            PatchReplicaVars(fields, type);

            if (!InheritsTypeFrom(type, "Cube.Replication.ReplicaBehaviour")) {
                Debug.LogError("VAR Patcher: ReplicaVar on type '" + type.FullName + "' which is not derived from Cube.Networking.Replicas.ReplicaBehaviour");
                return;
            }

            PatchSerialize(fields, type);
            PatchDeserialize(fields, type);
            PatchHasReplicaVarChanged(type);
        }

        void ProcessCallsites(TypeDefinition type) {
            foreach (var nestedType in type.NestedTypes) {
                ProcessCallsites(nestedType);
            }

            PatchReplicaVarAssignments(type);
        }

        List<FieldDefinition> FindReplicaVarFields(TypeDefinition type) {
            var fields = new List<FieldDefinition>();
            foreach (var field in type.Fields) {
                if (!HasAttribute("Cube.Replication.ReplicaVarAttribute", field))
                    continue;

                fields.Add(field);
            }
            return fields;
        }

        void PatchReplicaVars(List<FieldDefinition> fields, TypeDefinition type) {
            for (int i = 0; i < fields.Count; ++i) {
                var field = fields[i];
                InjectWrapperProperty(field, i, type);
            }
        }

        // Copy&Paste from Mono.Cecil.Mixin (private)
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

        // #todo bool argument, spank me I've been bad
        MethodReference GetBitStreamOverload(bool write, FieldDefinition field) {
            var fieldType = field.FieldType;

            var typeDefinition = fieldType.Resolve();
            if (typeDefinition == null)
                return null;

            if (typeDefinition.IsEnum)
                fieldType = GetEnumUnderlyingType(typeDefinition);

            MethodReference result = null;
            if (write)
                _bitStreamWrite.TryGetValue(fieldType.Name, out result);
            else
                _bitStreamRead.TryGetValue(fieldType.Name + "&", out result);

            return result;
        }

        void PatchSerialize(List<FieldDefinition> fields, TypeDefinition type) {
            if ((appType & ApplicationType.Server) == 0)
                return;

            var serializedMethodName = "Serialize";
            var serializedMethodFlags = MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public;

            var userDefinedSerializeMethod = GetMethodDefinitionByName(type, "Serialize");
            if (userDefinedSerializeMethod != null) {
                serializedMethodName = "Serialize__RpcVars__";
                serializedMethodFlags = MethodAttributes.HideBySig | MethodAttributes.Private;
            }

            var serialize = new MethodDefinition(serializedMethodName, serializedMethodFlags, module.TypeSystem.Void);
            serialize.Parameters.Add(new ParameterDefinition("bs", ParameterAttributes.None, ImportType(_bitStreamType)));
            serialize.Parameters.Add(new ParameterDefinition("mode", ParameterAttributes.None, _replicaSerializationModeType));
            serialize.Parameters.Add(new ParameterDefinition("view", ParameterAttributes.None, _replicaViewType));
            type.Methods.Add(serialize);

            if (userDefinedSerializeMethod != null) {
                var userIl = userDefinedSerializeMethod.Body.GetILProcessor();

                userIl.Replace(userIl.Body.Instructions.Last(), userIl.Create(OpCodes.Ldarg_0));
                userIl.Emit(OpCodes.Ldarg_1);
                userIl.Emit(OpCodes.Ldarg_2);
                userIl.Emit(OpCodes.Ldarg_3);
                userIl.Emit(OpCodes.Callvirt, ResolveMethodReference(serialize));
                userIl.Emit(OpCodes.Ret);
            }

            var il = serialize.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);

            /*
            if (mode == ReplicaSerializationMode.Partial)
            */
            var modeFullJumpTarget = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Brtrue, modeFullJumpTarget);

            for (int i = 0; i < fields.Count; ++i) {
                var field = fields[i];

                var bitStreamWriteOverload = GetBitStreamOverload(true, field);
                if (bitStreamWriteOverload == null) {
                    Debug.LogWarning("ReplicaVar '" + field.FullName + "' type not supported");
                    continue;
                }

                /*
                if ((dirtyFieldsMask & 1) == 1)
                */
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, _replicaDirtyFieldsMask);
                il.Emit(OpCodes.Ldc_I4, i + 1);
                il.Emit(OpCodes.Conv_I8);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Ldc_I4, i + 1);
                il.Emit(OpCodes.Conv_I8);

                var fieldNotDifferentJumpTarget = il.Create(OpCodes.Nop);
                il.Emit(OpCodes.Bne_Un, fieldNotDifferentJumpTarget);

                /*
                bs.Write(true);
                bs.Write(field);
                */
                var fooJumpTarget = il.Create(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Callvirt, _bitStreamWriteBool);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, bitStreamWriteOverload);
                il.Emit(OpCodes.Br, fooJumpTarget);

                /*
                else {
                    bs.Write(false);
                }
                */
                il.Append(fieldNotDifferentJumpTarget);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, _bitStreamWriteBool);

                il.Append(fooJumpTarget);
            }

            var endJumpTarget = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Br, endJumpTarget);
            il.Append(modeFullJumpTarget);

            for (int i = 0; i < fields.Count; ++i) {
                var field = fields[i];

                var bitStreamWriteOverload = GetBitStreamOverload(true, field);
                if (bitStreamWriteOverload == null)
                    continue;

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, bitStreamWriteOverload);
            }

            il.Append(endJumpTarget);
            il.Emit(OpCodes.Ret);
        }

        void PatchDeserialize(List<FieldDefinition> fields, TypeDefinition type) {
            var deserializedMethodName = "Deserialize";
            var deserializedMethodFlags = MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public;

            var userDefiniedDeserializeMethod = GetMethodDefinitionByName(type, "Deserialize");
            if (userDefiniedDeserializeMethod != null) {
                deserializedMethodName = "Deserialize__RpcVars__";
                deserializedMethodFlags = MethodAttributes.HideBySig | MethodAttributes.Private;
            }

            var deserialize = new MethodDefinition(deserializedMethodName, deserializedMethodFlags, module.TypeSystem.Void);
            deserialize.Parameters.Add(new ParameterDefinition("bs", ParameterAttributes.None, ImportType(_bitStreamType)));
            deserialize.Parameters.Add(new ParameterDefinition("mode", ParameterAttributes.None, _replicaSerializationModeType));
            type.Methods.Add(deserialize);

            if (userDefiniedDeserializeMethod != null) {
                var userIl = userDefiniedDeserializeMethod.Body.GetILProcessor();

                userIl.Replace(userIl.Body.Instructions.Last(), userIl.Create(OpCodes.Ldarg_0));
                userIl.Emit(OpCodes.Ldarg_1);
                userIl.Emit(OpCodes.Ldarg_2);
                userIl.Emit(OpCodes.Callvirt, ResolveMethodReference(deserialize));
                userIl.Emit(OpCodes.Ret);
            }

            var il = deserialize.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_2);
            var fullDeserializeJumpTarget = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Brtrue, fullDeserializeJumpTarget);

            foreach (var field in fields) {
                var bitStreamReadOverload = GetBitStreamOverload(false, field);

                //Debug.Log(field.FieldType.Name + " - " + bitStreamReadOverload);

                if (bitStreamReadOverload == null)
                    continue;

                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, _bitStreamReadBool);

                var notChangedJumpTarget = il.Create(OpCodes.Nop);
                il.Emit(OpCodes.Brfalse, notChangedJumpTarget);

                il.Emit(OpCodes.Nop);

                if (!field.DeclaringType.IsEnum) {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, field);
                    il.Emit(OpCodes.Callvirt, bitStreamReadOverload);
                } else {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, field);
                    il.Emit(OpCodes.Stloc_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldloca_S, 0);
                    il.Emit(OpCodes.Callvirt, bitStreamReadOverload);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Stfld, field);
                }

                il.Emit(OpCodes.Nop);

                il.Append(notChangedJumpTarget);
            }

            il.Emit(OpCodes.Ret);

            il.Append(fullDeserializeJumpTarget);
            foreach (var field in fields) {
                var bitStreamReadOverload = GetBitStreamOverload(false, field);
                if (bitStreamReadOverload == null)
                    continue;

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, field);
                il.Emit(OpCodes.Callvirt, bitStreamReadOverload);
            }
            il.Emit(OpCodes.Ret);
        }

        void PatchHasReplicaVarChanged(TypeDefinition type) {
            foreach (var method in type.Methods) {
                var instructions = method.Body.Instructions;
                for (int i = 0; i < instructions.Count; ++i) {
                    var instruction = instructions[i];
                    if (instruction.OpCode != OpCodes.Call)
                        continue;

                    var function = instruction.Operand as MethodReference;
                    if (function == null || function.Name != "HasReplicaVarChanged")
                        continue;

                    if (i >= 3 && instructions.Count < 5) {
                        // error
                        continue;
                    }

                    var jumpTargetInstruction = instructions[i + 1]; // #todo verify it's actually a jump
                    var jumpTarget = (Instruction)jumpTargetInstruction.Operand;

                    var il = method.Body.GetILProcessor();

                    for (int j = 0; j < 5; ++j) {
                        il.Remove(instructions[i - 3]);
                    }

                    var nextInstruction = instructions[i - 3];
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Ldfld, _replicaDirtyFieldsMask));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Ldc_I4, 1)); // #todo lookup index
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Conv_I8));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.And));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Ldc_I4, 1));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Conv_I8));
                    il.InsertBefore(nextInstruction, il.Create(OpCodes.Bne_Un, jumpTarget));

                    Debug.LogError("VAR Patcher: Found HasReplicaVarChanged call in " + type);

                    /*
                     REPLACE
                        IL_005a: ldarg.0
                        IL_005b: ldarg.0
                        IL_005c: ldfld float32 ReplicaVarTest::_posY
                        IL_0061: call instance bool [CubePlugin]Cube.ReplicaBehaviour::HasReplicaVarChanged<float32>(!!0)
                        IL_0066: brfalse IL_006d

                    WITH
                        IL_006d: ldarg.0
                        IL_006e: ldfld uint64 [CubePlugin]Cube.ReplicaBehaviour::dirtyFieldsMask
                        IL_0073: ldc.i4.1
                        IL_0074: conv.i8
                        IL_0075: and
                        IL_0076: ldc.i4.1
                        IL_0077: conv.i8
                        IL_0078: bne.un IL_007f

                    */
                }
            }
        }

        void InjectWrapperProperty(FieldDefinition field, int fieldIdx, TypeDefinition type) {
            var propName = field.Name + "_Prop";
            if (type.Properties.FirstOrDefault(m => m.Name == propName) != null) // Property already present
                return;

            MethodDefinition setter;
            {
                setter = new MethodDefinition("set_" + propName, MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public, module.TypeSystem.Void);
                setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, field.FieldType));

                var il = setter.Body.GetILProcessor();
                il.Emit(OpCodes.Nop);

                // Set dirty
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, field);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldfld, _replicaDirtyFieldsMask);
                il.Emit(OpCodes.Ldc_I4, fieldIdx + 1); // #todo load directly as int64
                il.Emit(OpCodes.Conv_I8);
                il.Emit(OpCodes.Or);
                il.Emit(OpCodes.Stfld, _replicaDirtyFieldsMask);
                il.Emit(OpCodes.Ret);

                type.Methods.Add(setter);
            }

            _setters.Add(field, setter);

            MethodDefinition getter;
            {
                getter = new MethodDefinition("get_" + propName, MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public, field.FieldType);

                getter.Body.Variables.Add(new VariableDefinition(field.FieldType));

                var il = getter.Body.GetILProcessor();
                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Stloc_0);
                var foo = il.Create(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Br, foo);
                il.Append(foo);
                il.Emit(OpCodes.Ret);

                type.Methods.Add(getter);
            }

            var propertyDefinition = new PropertyDefinition(propName, PropertyAttributes.None, field.FieldType) {
                GetMethod = getter,
                SetMethod = setter
            };

            type.Properties.Add(propertyDefinition);
        }

        void PatchReplicaVarAssignments(TypeDefinition type) {
            foreach (var method in type.Methods) {
                if (_setters.ContainsValue(method)) // Ignore the setters themselves
                    continue;

                if (method.Body == null)
                    continue;

                for (int i = 0; i < method.Body.Instructions.Count; ++i) {
                    var instruction = method.Body.Instructions[i];
                    if (instruction.OpCode != OpCodes.Stfld)
                        continue;

                    var field = instruction.Operand as FieldDefinition;
                    if (field == null)
                        continue;

                    foreach (var customAttribute in field.CustomAttributes) {
                        if (customAttribute.AttributeType.Name != _replicaVarAttributeType.Name)
                            continue;

                        var setter = _setters[field];

                        var il = method.Body.GetILProcessor();
                        var newCall = il.Create(OpCodes.Call, setter);

                        il.Replace(instruction, newCall);
                    }
                }
            }
        }
    }
}

#endif
