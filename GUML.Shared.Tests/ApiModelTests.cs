using GUML.Shared.Api;

namespace GUML.Shared.Tests;

[TestClass]
public class ApiModelTests
{
    // ================================================================
    // ApiDocument tests
    // ================================================================

    [TestMethod]
    public void ApiDocument_DefaultValues()
    {
        var doc = new ApiDocument();
        Assert.AreEqual("", doc.SchemaVersion);
        Assert.IsNotNull(doc.Types);
        Assert.AreEqual(0, doc.Types.Count);
        Assert.IsNotNull(doc.Controllers);
        Assert.AreEqual(0, doc.Controllers.Count);
    }

    [TestMethod]
    public void ApiDocument_CanSetProperties()
    {
        var doc = new ApiDocument
        {
            SchemaVersion = "1.0",
            SdkVersion = "4.6.1",
            GeneratedAt = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc)
        };
        Assert.AreEqual("1.0", doc.SchemaVersion);
        Assert.AreEqual("4.6.1", doc.SdkVersion);
    }

    // ================================================================
    // TypeDescriptor tests
    // ================================================================

    [TestMethod]
    public void TypeDescriptor_DefaultValues()
    {
        var td = new TypeDescriptor();
        Assert.AreEqual("", td.Name);
        Assert.AreEqual("", td.QualifiedName);
        Assert.AreEqual(GumlTypeKind.Class, td.Kind);
    }

    [TestMethod]
    public void TypeDescriptor_ClassKind()
    {
        var td = new TypeDescriptor
        {
            Name = "Label", QualifiedName = "Godot.Label", Kind = GumlTypeKind.Class, BaseType = "Control"
        };
        Assert.AreEqual("Label", td.Name);
        Assert.AreEqual("Godot.Label", td.QualifiedName);
        Assert.AreEqual(GumlTypeKind.Class, td.Kind);
        Assert.AreEqual("Control", td.BaseType);
    }

    [TestMethod]
    public void TypeDescriptor_StructKind()
    {
        var td = new TypeDescriptor { Name = "Vector2", QualifiedName = "Godot.Vector2", Kind = GumlTypeKind.Struct };
        Assert.AreEqual(GumlTypeKind.Struct, td.Kind);
    }

    [TestMethod]
    public void TypeDescriptor_WithProperties()
    {
        var td = new TypeDescriptor
        {
            Name = "Label",
            QualifiedName = "Godot.Label",
            Properties =
            {
                ["text"] = new PropertyDescriptor
                {
                    Name = "text", Type = "System.String", IsReadable = true, IsWritable = true
                }
            }
        };
        Assert.AreEqual(1, td.Properties.Count);
        Assert.AreEqual("text", td.Properties["text"].Name);
    }

    [TestMethod]
    public void TypeDescriptor_WithEvents()
    {
        var td = new TypeDescriptor
        {
            Name = "Button",
            QualifiedName = "Godot.Button",
            Events =
            {
                ["pressed"] = new EventDescriptor
                {
                    Name = "pressed", Description = "Fired when button is pressed"
                }
            }
        };
        Assert.AreEqual(1, td.Events.Count);
        Assert.AreEqual("pressed", td.Events["pressed"].Name);
    }

    // ================================================================
    // PropertyDescriptor tests
    // ================================================================

    [TestMethod]
    public void PropertyDescriptor_DefaultValues()
    {
        var pd = new PropertyDescriptor();
        Assert.AreEqual("", pd.Name);
        Assert.AreEqual("", pd.Type);
        Assert.AreEqual("", pd.Description);
        Assert.IsFalse(pd.IsReadable);
        Assert.IsFalse(pd.IsWritable);
        Assert.IsNull(pd.EnumValues);
        Assert.IsNotNull(pd.Mapping);
    }

    [TestMethod]
    public void PropertyDescriptor_ReadWriteFlags()
    {
        var pd = new PropertyDescriptor { Name = "text", Type = "System.String", IsReadable = true, IsWritable = true };
        Assert.IsTrue(pd.IsReadable);
        Assert.IsTrue(pd.IsWritable);
    }

    [TestMethod]
    public void PropertyDescriptor_WithEnumValues()
    {
        var pd = new PropertyDescriptor
        {
            Name = "horizontal_alignment",
            Type = "Godot.HorizontalAlignment",
            EnumValues =
            [
                new EnumValueDescriptor { Name = "Left", Value = "0" },
                new EnumValueDescriptor { Name = "Center", Value = "1" },
                new EnumValueDescriptor { Name = "Right", Value = "2" }
            ]
        };
        Assert.IsNotNull(pd.EnumValues);
        Assert.AreEqual(3, pd.EnumValues!.Count);
        Assert.AreEqual("Center", pd.EnumValues[1].Name);
        Assert.AreEqual("1", pd.EnumValues[1].Value);
    }

    // ================================================================
    // MappingConstraintDescriptor tests
    // ================================================================

    [TestMethod]
    public void MappingConstraint_DefaultValues()
    {
        var mc = new MappingConstraintDescriptor();
        Assert.IsFalse(mc.CanStaticMap);
        Assert.IsFalse(mc.CanBindDataToProperty);
        Assert.IsFalse(mc.CanBindPropertyToData);
        Assert.IsFalse(mc.CanBindTwoWay);
        Assert.IsFalse(mc.IsObservableProperty);
        Assert.AreEqual(ObservabilitySource.None, mc.ObservabilitySource);
    }

    [TestMethod]
    public void MappingConstraint_TwoWayBinding_ConsistencyCheck()
    {
        // If CanBindTwoWay is true, both directions should logically be supported
        var mc = new MappingConstraintDescriptor
        {
            CanStaticMap = true,
            CanBindDataToProperty = true,
            CanBindPropertyToData = true,
            CanBindTwoWay = true,
            IsObservableProperty = true,
            ObservabilitySource = ObservabilitySource.Signal
        };
        // Verify two-way implies both directions
        Assert.IsTrue(mc.CanBindTwoWay);
        Assert.IsTrue(mc.CanBindDataToProperty);
        Assert.IsTrue(mc.CanBindPropertyToData);
    }

    [TestMethod]
    public void MappingConstraint_ObservabilitySource_AllValues()
    {
        // Verify all enum members are defined and have distinct values
        var values = (ObservabilitySource[])Enum.GetValues(typeof(ObservabilitySource));
        Assert.AreEqual(4, values.Length);
        CollectionAssert.AllItemsAreUnique(values);
        Assert.IsTrue(Enum.IsDefined(typeof(ObservabilitySource), ObservabilitySource.None));
        Assert.IsTrue(Enum.IsDefined(typeof(ObservabilitySource), ObservabilitySource.NotifyPropertyChanged));
        Assert.IsTrue(Enum.IsDefined(typeof(ObservabilitySource), ObservabilitySource.Signal));
        Assert.IsTrue(Enum.IsDefined(typeof(ObservabilitySource), ObservabilitySource.Custom));
    }

    // ================================================================
    // EventDescriptor tests
    // ================================================================

    [TestMethod]
    public void EventDescriptor_DefaultValues()
    {
        var ed = new EventDescriptor();
        Assert.AreEqual("", ed.Name);
        Assert.IsNull(ed.Description);
        Assert.IsNotNull(ed.Parameters);
        Assert.AreEqual(0, ed.Parameters.Count);
    }

    [TestMethod]
    public void EventDescriptor_WithParameters()
    {
        var ed = new EventDescriptor
        {
            Name = "text_changed",
            Parameters = [new ParameterDescriptor { Name = "newText", Type = "System.String" }]
        };
        Assert.AreEqual(1, ed.Parameters.Count);
        Assert.AreEqual("newText", ed.Parameters[0].Name);
        Assert.AreEqual("System.String", ed.Parameters[0].Type);
    }

    // ================================================================
    // ControllerDescriptor tests
    // ================================================================

    [TestMethod]
    public void ControllerDescriptor_DefaultValues()
    {
        var cd = new ControllerDescriptor();
        Assert.AreEqual("", cd.FullName);
        Assert.AreEqual("", cd.SimpleName);
        Assert.AreEqual("", cd.GumlPath);
        Assert.IsNotNull(cd.Properties);
        Assert.IsNotNull(cd.Methods);
    }

    [TestMethod]
    public void ControllerDescriptor_FullSetup()
    {
        var cd = new ControllerDescriptor
        {
            FullName = "MyApp.MainController",
            SimpleName = "MainController",
            GumlPath = "res://gui/main.guml",
            Properties =
            [
                new ParameterDescriptor { Name = "Title", Type = "System.String" },
                new ParameterDescriptor { Name = "Count", Type = "System.Int32" }
            ],
            Methods =
            [
                new MethodDescriptor { Name = "OnClick" },
                new MethodDescriptor { Name = "OnLoad" }
            ]
        };
        Assert.AreEqual("MainController", cd.SimpleName);
        Assert.AreEqual(2, cd.Properties.Count);
        Assert.AreEqual(2, cd.Methods.Count);
        Assert.IsTrue(cd.Methods.Any(m => m.Name == "OnClick"));
    }

    // ================================================================
    // EnumValueDescriptor tests
    // ================================================================

    [TestMethod]
    public void EnumValueDescriptor_DefaultValues()
    {
        var ev = new EnumValueDescriptor();
        Assert.AreEqual("", ev.Name);
        Assert.AreEqual("", ev.Value);
        Assert.IsNull(ev.Description);
    }

    [TestMethod]
    public void EnumValueDescriptor_WithDescription()
    {
        var ev = new EnumValueDescriptor { Name = "Center", Value = "1", Description = "Center alignment" };
        Assert.AreEqual("Center", ev.Name);
        Assert.AreEqual("1", ev.Value);
        Assert.AreEqual("Center alignment", ev.Description);
    }

    // ================================================================
    // ParameterDescriptor tests
    // ================================================================

    [TestMethod]
    public void ParameterDescriptor_DefaultValues()
    {
        var pd = new ParameterDescriptor();
        Assert.AreEqual("", pd.Name);
        Assert.AreEqual("", pd.Type);
    }

    // ================================================================
    // Integration: Full ApiDocument roundtrip
    // ================================================================

    [TestMethod]
    public void ApiDocument_FullRoundtrip()
    {
        var doc = new ApiDocument
        {
            SchemaVersion = "1.0",
            SdkVersion = "4.6.1",
            GeneratedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Add a type
        var labelType = new TypeDescriptor
        {
            Name = "Label",
            QualifiedName = "Godot.Label",
            Kind = GumlTypeKind.Class,
            BaseType = "Control",
            Description = "A label node",
            Properties =
            {
                ["text"] = new PropertyDescriptor
                {
                    Name = "text",
                    Type = "System.String",
                    IsReadable = true,
                    IsWritable = true,
                    Mapping = new MappingConstraintDescriptor
                    {
                        CanStaticMap = true,
                        CanBindDataToProperty = true,
                        CanBindPropertyToData = false,
                        CanBindTwoWay = false,
                        IsObservableProperty = false,
                        ObservabilitySource = ObservabilitySource.None
                    }
                }
            },
            Events = { ["visibility_changed"] = new EventDescriptor { Name = "visibility_changed" } }
        };
        doc.Types["Godot.Label"] = labelType;

        // Add a controller
        doc.Controllers["MyApp.MainController"] = new ControllerDescriptor
        {
            FullName = "MyApp.MainController",
            SimpleName = "MainController",
            GumlPath = "res://gui/main.guml",
            Properties =
            [
                new ParameterDescriptor { Name = "Title", Type = "System.String" }
            ],
            Methods = [new MethodDescriptor { Name = "OnClick" }]
        };

        // Verify structure is intact
        Assert.AreEqual(1, doc.Types.Count);
        Assert.AreEqual(1, doc.Controllers.Count);
        Assert.IsTrue(doc.Types.ContainsKey("Godot.Label"));
        Assert.IsTrue(doc.Controllers.ContainsKey("MyApp.MainController"));
        var retrieved = doc.Types["Godot.Label"];
        Assert.AreEqual("Label", retrieved.Name);
        Assert.AreEqual(1, retrieved.Properties.Count);
        Assert.IsTrue(retrieved.Properties["text"].Mapping.CanStaticMap);
        Assert.IsFalse(retrieved.Properties["text"].Mapping.CanBindTwoWay);
    }
}
