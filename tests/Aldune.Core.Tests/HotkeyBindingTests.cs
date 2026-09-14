using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void Default_IsCtrlShiftN()
    {
        // Ctrl+Alt+N se descarto a proposito: en muchos teclados esta tomada por controles
        // multimedia, y Windows no comparte atajos — el primero que lo pide se lo queda.
        Assert.Equal("Ctrl + Shift + N", HotkeyBinding.Default.DisplayName);
    }

    [Fact]
    public void DisplayName_UsesTheConventionalModifierOrder()
    {
        // Ctrl, Alt, Shift, Win — el orden de la documentacion de Windows, no el de los bits.
        var all = new HotkeyBinding(
            HotkeyBinding.ModWin | HotkeyBinding.ModShift | HotkeyBinding.ModAlt | HotkeyBinding.ModControl,
            0x41);
        Assert.Equal("Ctrl + Alt + Shift + Win + A", all.DisplayName);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x20, "Espacio")]
    [InlineData(0x2E, "Supr")]
    public void DisplayName_NamesTheCommonKeys(uint key, string expected)
    {
        var binding = new HotkeyBinding(HotkeyBinding.ModControl, key);
        Assert.Equal($"Ctrl + {expected}", binding.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToTheCodeForKeysItCannotName()
    {
        // Mejor un codigo que un nombre inventado: al menos es cierto y se puede buscar.
        var binding = new HotkeyBinding(HotkeyBinding.ModControl, 0xFF);
        Assert.Equal("Ctrl + 0xFF", binding.DisplayName);
    }

    [Fact]
    public void WithoutModifiers_IsNotValid()
    {
        // Un atajo global sin modificador se tragaria esa tecla en todo el sistema.
        Assert.False(new HotkeyBinding(0, 0x4E).IsValid);
    }

    [Fact]
    public void WithoutAKey_IsNotValid()
    {
        Assert.False(new HotkeyBinding(HotkeyBinding.ModControl, 0).IsValid);
    }

    [Fact]
    public void InvalidBinding_SaysSoInsteadOfShowingHalfACombination()
    {
        Assert.Equal("sin asignar", new HotkeyBinding(0, 0).DisplayName);
    }

    [Fact]
    public void Default_IsValid()
    {
        Assert.True(HotkeyBinding.Default.IsValid);
    }
}
