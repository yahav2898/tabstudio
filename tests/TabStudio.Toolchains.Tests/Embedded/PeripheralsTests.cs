using System.Xml;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class PeripheralsTests
{
    [Fact]
    public void Svd_resolves_register_and_peripheral_inheritance_and_decodes_named_bits()
    {
        var device = SvdReader.Parse("""
            <device><name>TestChip</name><size>32</size><peripherals>
              <peripheral><name>GPIOA</name><baseAddress>0x40000000</baseAddress><registers>
                <register><name>CTRL</name><description>Control</description><addressOffset>0x10</addressOffset><fields>
                  <field><name>MODE</name><bitOffset>4</bitOffset><bitWidth>2</bitWidth><enumeratedValues>
                    <enumeratedValue><name>Output</name><value>1</value><description>Drive pin</description></enumeratedValue>
                  </enumeratedValues></field>
                </fields></register>
                <register derivedFrom="CTRL"><name>CTRL2</name><addressOffset>0x14</addressOffset></register>
              </registers></peripheral>
              <peripheral derivedFrom="GPIOA"><name>GPIOB</name><baseAddress>0x50000000</baseAddress></peripheral>
            </peripherals></device>
            """);
        Assert.Equal("TestChip", device.Name);
        var register = device.Peripherals[1].Registers[1];
        Assert.Equal(0x50000014UL, register.Address);
        Assert.Equal("CTRL2", register.Name);
        Assert.Equal(1UL, register.Fields[0].Decode(0x10));
        Assert.Contains("Drive pin", RegisterAccess.Describe(device.Peripherals[1], register));
    }

    [Fact]
    public void Svd_expands_cluster_register_and_field_arrays()
    {
        var device = SvdReader.Parse(Wrap("""
            <cluster><name>CH%s</name><addressOffset>0x100</addressOffset><dim>2</dim><dimIncrement>0x20</dimIncrement><dimIndex>A-B</dimIndex>
              <register><name>R%s</name><addressOffset>0</addressOffset><dim>2</dim><dimIncrement>4</dimIncrement><fields>
                <field><name>BIT%s</name><bitOffset>0</bitOffset><bitWidth>1</bitWidth><dim>2</dim><dimIncrement>1</dimIncrement></field>
              </fields></register>
            </cluster>
            """));
        var registers = device.Peripherals[0].Registers;
        Assert.Equal(4, registers.Count);
        Assert.Equal("CHB.R1", registers[3].Name);
        Assert.Equal(0x40000124UL, registers[3].Address);
        Assert.Equal("BIT1", registers[3].Fields[1].Name);
        Assert.Equal(1, registers[3].Fields[1].BitOffset);
    }

    [Fact]
    public void Svd_supports_bitRange_full_width_and_reset_values()
    {
        var register = Assert.Single(SvdReader.Parse(Wrap("""
            <register><name>FULL</name><addressOffset>0</addressOffset><size>64</size><resetValue>0x1122</resetValue><fields>
              <field><name>VALUE</name><bitRange>[63:0]</bitRange></field>
            </fields></register>
            """)).Peripherals[0].Registers);
        Assert.Equal(ulong.MaxValue, register.Fields[0].Mask);
        Assert.Equal(0x1122UL, register.ResetValue);
    }

    [Fact]
    public void Svd_rejects_external_entities_and_recursive_inheritance()
    {
        Assert.Throws<XmlException>(() => SvdReader.Parse("<!DOCTYPE device [<!ENTITY leak SYSTEM 'file:///etc/passwd'>]><device><name>&leak;</name></device>"));
        Assert.Throws<FormatException>(() => SvdReader.Parse(Wrap("""
            <register derivedFrom="B"><name>A</name><addressOffset>0</addressOffset></register>
            <register derivedFrom="A"><name>B</name><addressOffset>4</addressOffset></register>
            """)));
    }

    [Theory]
    [InlineData("<field><name>A</name><lsb>4</lsb><msb>3</msb></field>")]
    [InlineData("<field><name>A</name><bitOffset>31</bitOffset><bitWidth>2</bitWidth></field>")]
    [InlineData("<field><name>A</name><bitOffset>0</bitOffset><bitWidth>2</bitWidth></field><field><name>B</name><bitOffset>1</bitOffset><bitWidth>1</bitWidth></field>")]
    public void Svd_rejects_invalid_or_overlapping_field_ranges(string fields)
    {
        Assert.Throws<FormatException>(() => SvdReader.Parse(Wrap("<register><name>R</name><addressOffset>0</addressOffset><fields>" + fields + "</fields></register>")));
    }

    [Fact]
    public void Field_write_preserves_neighbouring_bits_and_rejects_overflow()
    {
        var field = new SvdField("MODE", "", 4, 2, "read-write", null, null, []);
        var register = new SvdRegister("CTRL", "", 0x40000000, 32, "read-write", null, null, null, [field]);
        Assert.Equal(0xFFFFFFDFUL, RegisterAccess.CreateFieldWrite(register, field, 1, uint.MaxValue).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => RegisterAccess.CreateFieldWrite(register, field, 4, 0));
        Assert.Equal("(0x1U << 4)", RegisterAccess.MaskExpression(field, 1));
    }

    [Theory]
    [InlineData("clear", null)]
    [InlineData(null, "oneToClear")]
    [InlineData(null, "zeroToSet")]
    public void Field_write_refuses_register_side_effects(string? readAction, string? writeAction)
    {
        var field = new SvdField("READY", "", 0, 1, "read-write", writeAction, readAction, []);
        var register = new SvdRegister("STATUS", "", 0, 32, "read-write", null, writeAction, readAction, [field]);
        Assert.Throws<InvalidOperationException>(() => RegisterAccess.CreateFieldWrite(register, field, 1, 0));
    }

    [Fact]
    public void Explicit_W1C_action_does_not_read_and_mixed_registers_are_refused()
    {
        var field = new SvdField("READY", "", 0, 1, "read-write", "oneToClear", null, []);
        var register = new SvdRegister("STATUS", "", 0x40000000, 32, "read-write", null, "oneToClear", null, [field]);
        var write = RegisterAccess.CreateFieldAction(register, field, 1);
        Assert.Equal(1UL, write.Value);
        Assert.Contains("oneToClear", write.Description);
        var ordinary = new SvdField("ENABLE", "", 1, 1, "read-write", null, null, []);
        Assert.Throws<InvalidOperationException>(() => RegisterAccess.CreateFieldAction(register with { Fields = [field, ordinary] }, field, 1));
        Assert.Throws<InvalidOperationException>(() => RegisterAccess.CreateFieldAction(register with { ModifiedWriteValues = null }, field, 1));
    }

    [Fact]
    public void Cube_ioc_conflicts_retain_both_source_locations_and_check_actual_package_map()
    {
        var pins = PinConflictChecker.ReadCubeIoc("PA0.Signal=USART2_TX\nPA0.Signal=TIM2_CH1\nPB99.Signal=SPI1_SCK\nPA1.Locked=true", "board.ioc");
        var diagnostics = PinConflictChecker.Check(pins, new HashSet<string> { "PA0", "PA1" });
        Assert.Equal(3, diagnostics.Count);
        Assert.Contains(diagnostics, d => d.Code == "TSEMBPIN001" && d.Line == 1 && d.Message.Contains("USART2", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Code == "TSEMBPIN001" && d.Line == 2 && d.Message.Contains("TIM2", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Code == "TSEMBPIN002" && d.Line == 3);
    }

    [Fact]
    public void Devicetree_literal_macros_preserve_enclosing_pinctrl_owner()
    {
        var pins = PinConflictChecker.ReadDevicetree("""
            uart0_default: uart0_default {
              group1 {
                psels = <NRF_PSEL(UART_TX, 0, 6)>;
              };
            };
            spi_default: spi_default {
              pinmux = <STM32_PINMUX('A', 9, AF7)>;
            };
            """, "board.dts");
        Assert.Equal("P0.6", pins[0].Pin);
        Assert.Equal("uart0_default", pins[0].Owner);
        Assert.Equal("PA9", pins[1].Pin);
        Assert.Equal(7, pins[1].Line);
    }

    [Fact]
    public void Arduino_checks_explicit_pin_roles_without_treating_repeated_use_as_a_conflict()
    {
        var pins = PinConflictChecker.ReadArduino("""
            #define LED_PIN 13
            const int RELAY_PIN = 13;
            // pinMode(99, OUTPUT);
            pinMode(LED_PIN, OUTPUT);
            digitalWrite(LED_PIN, HIGH);
            pinMode(RELAY_PIN, OUTPUT);
            digitalRead(computedPin);
            """, "blink.ino");
        Assert.Equal(3, pins.Count);
        Assert.Equal(4, pins[0].Line);
        Assert.Equal(3, PinConflictChecker.Check(pins).Count);
        Assert.Empty(PinConflictChecker.Check(pins.Take(2)));
    }

    private static string Wrap(string registers) => "<device><name>Test</name><peripherals><peripheral><name>P</name><baseAddress>0x40000000</baseAddress><registers>" + registers + "</registers></peripheral></peripherals></device>";
}
