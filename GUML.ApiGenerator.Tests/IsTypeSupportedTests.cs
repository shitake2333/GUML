using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using GUML.ApiGenerator;

namespace GUML.ApiGenerator.Tests;

[TestClass]
public class IsTypeSupportedTests
{
    private static readonly ApiModelBuilder Builder = new();

    [TestMethod]
    public void PrimitiveBoolean_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<bool>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void GodotStruct_Vector2_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<Godot.Vector2>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void GodotReference_NodePath_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<Godot.NodePath>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void GodotReference_Array_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<Godot.Collections.Array>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void GodotResourceDerived_Texture2D_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<Godot.Texture2D>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void GodotEnum_IsSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<Godot.HorizontalAlignment>();
        Assert.IsTrue(Builder.IsTypeSupported(type));
    }

    [TestMethod]
    public void UnknownType_IsNotSupported()
    {
        var type = CecilTypeHelper.GetTypeReference<DateTimeOffset>();
        Assert.IsFalse(Builder.IsTypeSupported(type));
    }
}

internal static class CecilTypeHelper
{
    public static TypeReference GetTypeReference<T>()
    {
        var assemblyPath = typeof(T).Assembly.Location;
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
        var fullName = typeof(T).FullName;
        return assembly.MainModule.GetType(fullName)
            ?? throw new InvalidOperationException($"Unable to resolve type {fullName} for tests.");
    }
}
