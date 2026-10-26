using System.Buffers.Binary;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class FaultCaptureTests
{
    [Theory]
    [InlineData(0xFFFFFFF9u, 0x20001000u, false, 32)]
    [InlineData(0xFFFFFFFDu, 0x20002000u, false, 32)]
    [InlineData(0xFFFFFFE9u, 0x20001000u, true, 104)]
    [InlineData(0xFFFFFFEDu, 0x20002000u, true, 104)]
    public void Exception_frame_selects_actual_stack_and_keeps_core_words_before_fp_context(uint lr, uint address, bool fp, int extent)
    {
        var plan = CortexFaultCapture.SelectFrame("cortex-m-v7", lr, 0x20001000, 0x20002000, true);
        Assert.Equal(address, plan.BasicFrameAddress);
        Assert.Equal(fp, plan.HasFloatingPointContext);
        Assert.Equal(extent, plan.FrameBytes);
    }

    [Theory]
    [InlineData(0x08000001u, true)]
    [InlineData(0xFFFFFFFDu, false)]
    [InlineData(0xFFFFFFF5u, true)]
    public void Arbitrary_handler_lr_unknown_returns_and_nonentry_pc_do_not_guess_a_stack(uint lr, bool atEntry)
    {
        Assert.Null(CortexFaultCapture.SelectFrame("cortex-m-v7", lr, 0x20001000, 0x20002000, atEntry).BasicFrameAddress);
    }

    [Theory]
    [InlineData(0xFFFFFFB9u)]
    [InlineData(0xFFFFFFF8u)]
    [InlineData(0xFFFFFF99u)]
    public void V8_security_transition_and_additional_callee_contexts_are_not_decoded(uint lr)
    {
        Assert.Null(CortexFaultCapture.SelectFrame("cortex-m-v8-mainline", lr, 0x20001000, 0x20002000, true).BasicFrameAddress);
    }

    [Fact]
    public void V8_same_nonsecure_context_has_explicit_known_layout()
    {
        Assert.Equal(0x20002000u, CortexFaultCapture.SelectFrame("cortex-m-v8-mainline", 0xFFFFFFBC, 0x20001000, 0x20002000, true).BasicFrameAddress);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x20000001u)]
    [InlineData(0xFFFFFFFCu)]
    public void Invalid_stack_ranges_are_refused(uint address)
    {
        Assert.Null(CortexFaultCapture.SelectFrame("cortex-m-v7", 0xFFFFFFF9, address, 0, true).BasicFrameAddress);
    }

    [Fact]
    public async Task Real_status_addresses_and_extended_frame_core_layout_are_read_without_any_target_write()
    {
        var reader = new Reader();
        var result = await CortexFaultCapture.CaptureAsync(reader, "cortex-m-v7");
        Assert.Equal(1u << 24, result.Snapshot.Cfsr);
        Assert.Equal(1u << 30, result.Snapshot.Hfsr);
        Assert.Equal(0x12345678u, result.Snapshot.Mmfar);
        Assert.Equal(0x87654321u, result.Snapshot.Bfar);
        Assert.Equal(0x20002000u, result.Snapshot.StackPointer);
        Assert.Equal(0x08001235u, result.Snapshot.StackedWords![6]);
        Assert.True(result.Frame.HasFloatingPointContext);
        Assert.Equal(new (uint, int)[] { (0xE000ED00, 12), (0xE000ED28, 20), (0x0800000c, 4), (0x20002000, 32) }, reader.Reads);
        Assert.Equal(new[] { CortexFaultRegister.Link, CortexFaultRegister.MainStack, CortexFaultRegister.ProcessStack, CortexFaultRegister.ProgramCounter }, reader.RegisterReads);
    }

    [Fact]
    public async Task A_stopped_pc_after_handler_prologue_keeps_status_but_has_no_fabricated_frame()
    {
        var reader = new Reader { Pc = 0x08000108 };
        var result = await CortexFaultCapture.CaptureAsync(reader, "cortex-m-v7");
        Assert.Null(result.Snapshot.StackedWords);
        Assert.Null(result.Frame.BasicFrameAddress);
        Assert.DoesNotContain(reader.Reads, read => read.Address == 0x20002000);
    }

    [Fact]
    public async Task A_stacking_fault_refuses_to_decode_even_a_plausible_frame()
    {
        var reader = new Reader { Cfsr = 1u << 12 };
        var result = await CortexFaultCapture.CaptureAsync(reader, "cortex-m-v7");
        Assert.Null(result.Snapshot.StackedWords);
        Assert.Contains("unreliable", result.Frame.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_models_and_cortex_m0_never_read_cfsr()
    {
        var reader = new Reader();
        await Assert.ThrowsAsync<NotSupportedException>(() => CortexFaultCapture.CaptureAsync(reader, "guess"));
        Assert.Empty(reader.Reads);
        reader.CpuId = 0x410CC200;
        await Assert.ThrowsAsync<NotSupportedException>(() => CortexFaultCapture.CaptureAsync(reader, "cortex-m-v7"));
        Assert.Equal((0xE000ED00u, 12), Assert.Single(reader.Reads));
    }

    [Fact]
    public void Debug_profile_requires_an_explicit_supported_fault_model()
    {
        var profile = EmbeddedDebugProfile.Parse("""
            {"firmware":"app.elf","interfaceConfig":"interface/stlink.cfg","targetConfig":"target/stm32f4x.cfg","faultModel":"cortex-m-v7"}
            """);
        Assert.Equal("cortex-m-v7", profile.FaultModel);
        Assert.Throws<InvalidDataException>(() => (profile with { FaultModel = "cortex-m0" }).Validate());
    }

    private sealed class Reader : ICortexFaultReader
    {
        public uint CpuId { get; set; } = 0x410FC241;
        public uint Pc { get; set; } = 0x08000100;
        public uint Cfsr { get; set; } = 1u << 24;
        public List<(uint Address, int Count)> Reads { get; } = [];
        public List<CortexFaultRegister> RegisterReads { get; } = [];

        public Task<byte[]> ReadMemoryAsync(uint address, int count, CancellationToken cancellationToken)
        {
            Reads.Add((address, count));
            uint[] words = address switch
            {
                0xE000ED00 => [CpuId, 3, 0x08000000],
                0xE000ED28 => [Cfsr, 1u << 30, 0, 0x12345678, 0x87654321],
                0x0800000c => [0x08000101],
                0x20002000 => [0, 1, 2, 3, 12, 0x08001241, 0x08001235, 1u << 24],
                _ => throw new InvalidOperationException("Unexpected memory request."),
            };
            var bytes = new byte[words.Length * 4];
            for (var i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
            Assert.Equal(count, bytes.Length);
            return Task.FromResult(bytes);
        }

        public Task<uint> ReadRegisterAsync(CortexFaultRegister registerKind, CancellationToken cancellationToken)
        {
            RegisterReads.Add(registerKind);
            return Task.FromResult(registerKind switch
            {
                CortexFaultRegister.Link => 0xFFFFFFEDu,
                CortexFaultRegister.MainStack => 0x20001000u,
                CortexFaultRegister.ProcessStack => 0x20002000u,
                CortexFaultRegister.ProgramCounter => Pc,
                _ => throw new ArgumentOutOfRangeException(nameof(registerKind)),
            });
        }
    }
}
