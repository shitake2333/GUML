using GUML.Shared.Converter;

namespace GUML.Tests;

[TestClass]
public class KeyConverterTests
{
    [TestMethod]
    public void ToPascalCase_SnakeCase_ConvertsProperly()
    {
        Assert.AreEqual("TextValue", KeyConverter.ToPascalCase("text_value"));
    }

    [TestMethod]
    public void ToPascalCase_KebabCase_ConvertsProperly()
    {
        Assert.AreEqual("TextValue", KeyConverter.ToPascalCase("text-value"));
    }

    [TestMethod]
    public void ToPascalCase_AlreadyPascalCase_Unchanged()
    {
        Assert.AreEqual("TextValue", KeyConverter.ToPascalCase("TextValue"));
    }

    [TestMethod]
    public void ToPascalCase_SingleWord_CapitalizesFirst()
    {
        Assert.AreEqual("Name", KeyConverter.ToPascalCase("name"));
    }

    [TestMethod]
    public void ToPascalCase_MultipleUnderscores_HandlesAll()
    {
        Assert.AreEqual("MyLongPropertyName", KeyConverter.ToPascalCase("my_long_property_name"));
    }

    [TestMethod]
    public void FromCamelCase_BasicConversion()
    {
        Assert.AreEqual("text_value", KeyConverter.FromCamelCase("TextValue"));
    }

    [TestMethod]
    public void FromCamelCase_SingleWord_LowersFirst()
    {
        Assert.AreEqual("name", KeyConverter.FromCamelCase("Name"));
    }

    [TestMethod]
    public void FromCamelCase_AllLowerCase_Unchanged()
    {
        Assert.AreEqual("name", KeyConverter.FromCamelCase("name"));
    }

    [TestMethod]
    public void FromCamelCase_MultipleUpperCase()
    {
        // Each uppercase letter gets an underscore prefix
        string result = KeyConverter.FromCamelCase("MyHTTPClient");
        Assert.AreEqual("my_h_t_t_p_client", result);
    }

}
