using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using UnityEngine;

/// <summary>
/// 
/// </summary>
/// <remarks>Available in: Editor</remarks>
abstract class PostProcessor {
    protected readonly ModuleDefinition MainModule;

    public PostProcessor(ModuleDefinition mainModule) {
        MainModule = mainModule;
    }

    public abstract bool Process(ModuleDefinition module);

    protected TypeReference Import(TypeDefinition type) => MainModule.ImportReference(type);

    protected MethodReference Import(MethodDefinition method) => MainModule.ImportReference(method);

    protected FieldReference Import(FieldDefinition field) => MainModule.ImportReference(field);

    protected TypeDefinition GetTypeDefinitionByName(AssemblyDefinition assembly, string typeName) => assembly.MainModule.GetType(typeName);

    protected MethodDefinition GetMethodDefinitionByName(TypeDefinition type, string methodName) {
        if (type == null)
            throw new ArgumentNullException("type");

        return type.Methods.FirstOrDefault(x => x.Name == methodName);
    }

    protected PropertyDefinition GetPropertyDefinitionByName(TypeDefinition type, string propertyName) {
        foreach (var property in type.Properties) {
            if (property.Name == propertyName)
                return property;
        }
        return null;
    }

    protected FieldDefinition GetFieldDefinitionByName(TypeDefinition type, string fieldName) {
        foreach (var field in type.Fields) {
            if (field.Name == fieldName)
                return field;
        }
        return null;
    }

    protected CustomAttribute GetAttributeByName(string attributeName, IEnumerable<CustomAttribute> customAttributes) {
        foreach (var attribute in customAttributes) {
            if (attribute.AttributeType.FullName == attributeName)
                return attribute;
        }
        return null;
    }

    protected bool HasAttribute(string attributeName, MethodDefinition method) {
        return GetAttributeByName(attributeName, method.CustomAttributes) != null;
    }

    protected bool IsDelegate(ParameterDefinition param) {
        TypeDefinition type = ResolveTypeReference(param.ParameterType);
        if (type == null || type.BaseType == null)
            return false;

        return type.BaseType.FullName == "System.MulticastDelegate";
    }

    protected bool TypeInheritsFrom(TypeDefinition type, string className) {
        if (type == null)
            return false;
        if (type.BaseType.FullName.StartsWith("System."))
            return false;

        var parentType = ResolveTypeReference(type.BaseType);

        do {
            if (parentType.FullName == className)
                return true;
            if (parentType.BaseType.FullName.StartsWith("System."))
                break;

            parentType = ResolveTypeReference(parentType.BaseType);
        } while (parentType != null);

        return false;
    }

    protected TypeDefinition ResolveTypeReference(TypeReference typeRef) {
        var typeDef = typeRef.Resolve();
        if (typeDef == null)
            throw new Exception("Cannot resolve type reference: " + typeRef);

        return typeDef;
    }

    protected void CopyMethodBody(MethodDefinition from, MethodDefinition to) {
        if (to.Body.Instructions.Count > 0) {
            to.Body.Instructions.Clear();
        }

        to.Body.InitLocals = from.Body.InitLocals;
        foreach (var variable in from.Body.Variables) {
            to.Body.Variables.Add(variable);
        }

        var il = to.Body.GetILProcessor();
        foreach (var instruction in from.Body.Instructions) {
            il.Append(instruction);
        }
    }

    protected AssemblyDefinition ResolveAssembly(string name) {
        var assembly = MainModule.AssemblyResolver.Resolve(new AssemblyNameReference(name, new Version()));
        if (assembly == null) {
            Debug.LogError($"Cannot resolve Assembly '{name}'");
        }
        return assembly;
    }
}
