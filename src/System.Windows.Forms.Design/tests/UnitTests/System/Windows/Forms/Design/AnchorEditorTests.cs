﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing.Design;
using Moq;
using System.Windows.Forms.TestUtilities;
using System.Reflection;

namespace System.Windows.Forms.Design.Tests;

public class AnchorEditorTests
{
    [Fact]
    public void AnchorEditor_Ctor_Default()
    {
        AnchorEditor editor = new();
        Assert.False(editor.IsDropDownResizable);
    }

    public static IEnumerable<object[]> EditValue_TestData()
    {
        yield return new object[] { null };
        yield return new object[] { "value" };
        yield return new object[] { AnchorStyles.Top };
        yield return new object[] { new object() };
    }

    [Theory]
    [MemberData(nameof(EditValue_TestData))]
    public void AnchorEditor_EditValue_ValidProvider_ReturnsValue(object value)
    {
        AnchorEditor editor = new();
        Mock<IWindowsFormsEditorService> mockEditorService = new(MockBehavior.Strict);
        Mock<IServiceProvider> mockServiceProvider = new(MockBehavior.Strict);
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IWindowsFormsEditorService)))
            .Returns(mockEditorService.Object)
            .Verifiable();
        mockEditorService
            .Setup(e => e.DropDownControl(It.IsAny<Control>()))
            .Verifiable();
        Assert.Equal(value, editor.EditValue(null, mockServiceProvider.Object, value));
        mockServiceProvider.Verify(p => p.GetService(typeof(IWindowsFormsEditorService)), Times.Once());
        mockEditorService.Verify(e => e.DropDownControl(It.IsAny<Control>()), Times.Once());

        // Edit again.
        Assert.Equal(value, editor.EditValue(null, mockServiceProvider.Object, value));
        mockServiceProvider.Verify(p => p.GetService(typeof(IWindowsFormsEditorService)), Times.Exactly(2));
        mockServiceProvider.Verify(p => p.GetService(typeof(IWindowsFormsEditorService)), Times.Exactly(2));
    }

    [Theory]
    [CommonMemberData(typeof(CommonTestHelperEx), nameof(CommonTestHelperEx.GetEditValueInvalidProviderTestData))]
    public void AnchorEditor_EditValue_InvalidProvider_ReturnsValue(IServiceProvider provider, object value)
    {
        AnchorEditor editor = new();
        Assert.Same(value, editor.EditValue(null, provider, value));
    }

    [Theory]
    [CommonMemberData(typeof(CommonTestHelperEx), nameof(CommonTestHelperEx.GetITypeDescriptorContextTestData))]
    public void AnchorEditor_GetEditStyle_Invoke_ReturnsModal(ITypeDescriptorContext context)
    {
        AnchorEditor editor = new();
        Assert.Equal(UITypeEditorEditStyle.DropDown, editor.GetEditStyle(context));
    }

    [Theory]
    [CommonMemberData(typeof(CommonTestHelperEx), nameof(CommonTestHelperEx.GetITypeDescriptorContextTestData))]
    public void AnchorEditor_GetPaintValueSupported_Invoke_ReturnsFalse(ITypeDescriptorContext context)
    {
        AnchorEditor editor = new();
        Assert.False(editor.GetPaintValueSupported(context));
    }

    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("top")]
    [InlineData("bottom")]
    public void AnchorEditor_AnchorUI_ControlType_IsCheckButton(string fieldName)
    {
        AnchorEditor editor = new();
        Type type = editor.GetType()
            .GetNestedType("AnchorUI", BindingFlags.NonPublic | BindingFlags.Instance);
        var anchorUI = (Control)Activator.CreateInstance(type, new object[] { editor });
        var item = (Control)anchorUI.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(anchorUI);

        object actual = item.AccessibilityObject.TestAccessor().Dynamic
            .GetPropertyValue(Interop.UiaCore.UIA.ControlTypePropertyId);

        Assert.Equal(Interop.UiaCore.UIA.CheckBoxControlTypeId, actual);
    }
}
