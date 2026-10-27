using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class PeripheralsMaskMacroTests
{
    [Theory]
    [InlineData("0x10U")]
    [InlineData("(1UL << 4)")]
    [InlineData("020")]
    public void Refactoring_uses_actual_device_header_definition_and_preserves_the_expression_span(string expression)
    {
        var macros = SvdMaskMacros.ParseHeader("""
            #define P_R_ENABLE_Pos (4U)
            #define P_R_ENABLE_Msk (0x1UL << P_R_ENABLE_Pos) /* Vendor definition */
            #define P_R_ENABLE P_R_ENABLE_Msk
            """);
        var source = "P->R |= " + expression + ";";
        var replacement = Assert.Single(macros.FindReplacements(Device(), source, 2));
        Assert.Equal(expression, source.Substring(replacement.Start, replacement.Length));
        Assert.Equal("P_R_ENABLE_Msk", replacement.Macro);
    }

    [Fact]
    public void Macro_value_mismatch_conditional_ambiguity_and_recursive_definitions_offer_no_fix()
    {
        var device = Device();
        const string source = "P->R |= 0x10;";
        Assert.Empty(SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk 0x20U").FindReplacements(device, source, 3));
        Assert.Empty(SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk 0x10U\n#define P_R_ENABLE_Msk 0x20U").FindReplacements(device, source, 3));
        Assert.Empty(SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk P_R_ENABLE\n#define P_R_ENABLE P_R_ENABLE_Msk").FindReplacements(device, source, 3));
        Assert.Empty(SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk(x) 0x10U").FindReplacements(device, source, 3));
    }

    [Theory]
    [InlineData("P->R |= USER_MASK;")]
    [InlineData("pointer->R |= 0x10;")]
    [InlineData("P->R |= 1U << 80;")]
    [InlineData("P->R |= do_work();")]
    [InlineData("P->R |= 0x20;")]
    public void Unresolved_user_symbols_pointer_aliases_invalid_shifts_and_other_bits_are_not_guessed(string source)
    {
        var macros = SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk (1U << 4)");
        Assert.Empty(macros.FindReplacements(Device(), source, 3));
    }

    [Theory]
    [InlineData("// P->R |= 0x10;")]
    [InlineData("// continued \\\n P->R |= 0x10;")]
    [InlineData("/* comment\n P->R |= 0x10;\n*/")]
    [InlineData("const char *s = \"P->R |= 0x10;\";")]
    [InlineData("auto s = R\"tag(P->R |= 0x10;)tag\";")]
    [InlineData("auto s = u8R\"(P->R |= 0x10;)\";")]
    public void Register_hints_and_mask_refactorings_reject_comments_and_quoted_examples(string source)
    {
        var position = source.IndexOf("R |=", StringComparison.Ordinal);
        Assert.False(SvdRegisterLookup.IsCodeAt(source, position));
        Assert.Null(SvdRegisterLookup.FindReference(Device(), source, position));
        Assert.Empty(SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk 0x10").FindReplacements(Device(), source, position));
    }

    [Fact]
    public void Empty_first_line_and_end_of_document_offsets_are_safe()
    {
        var macros = SvdMaskMacros.ParseHeader("#define P_R_ENABLE_Msk 0x10");
        Assert.Null(SvdRegisterLookup.FindReference(Device(), "\nP->R |= 0x10;", 0));
        Assert.Empty(macros.FindReplacements(Device(), "\nP->R |= 0x10;", 0));
        Assert.Null(SvdRegisterLookup.FindReference(Device(), "\n", 1));
    }

    private static SvdDevice Device()
    {
        var field = new SvdField("ENABLE", "Enable", 4, 1, "read-write", null, null, []);
        var register = new SvdRegister("R", "", 0, 32, "read-write", null, null, null, [field]);
        return new("Test", [new SvdPeripheral("P", "", 0, [register])]);
    }
}
