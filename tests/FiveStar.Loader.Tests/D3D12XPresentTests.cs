// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using FiveStar.Loader.Shims.D3D12X;
using FiveStar.Loader.Shims.D3D12X.Present;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>Queue slot 32 (PresentX) and device slots 78 and 80 (frame interval, frame-event wait), called as the game calls them.</summary>
public sealed unsafe class D3D12XPresentTests
{
    private const int PresentSlot = 32;
    private const int SetFrameIntervalSlot = 78;
    private const int WaitFrameEventSlot = 80;
    private const int CreateCommittedResourceSlot = 27;
    private const int DefaultHeap = 1;
    private const int Texture2D = 3;
    private const int Rgba8Unorm = 28;
    private const int AllowRenderTarget = 1;
    private const int RenderTargetState = 4;
    private const uint PopupStyle = 0x80000000;
    private const uint Infinite = 0xFFFFFFFF;
    private static readonly Guid ResourceIid = new("696442be-a72e-4059-bc79-5b5c98040fad");

    [Fact]
    public void Present_copies_a_render_target_into_a_swap_chain_on_a_hidden_window()
    {
        nint window = CreateHiddenWindow();

        try
        {
            using var test = TestRuntime.Create();
            var device = new XboxDevice(test);
            nint queue = device.Queue();
            nint texture = RenderTarget(device, 64, 32);
            test.Runtime.D3D12X.Presenter.Window = window;

            nint* resources = stackalloc nint[] { texture };
            var rect = new PlaneRect { Right = 64, Bottom = 32 };
            float* destination = stackalloc float[] { 0, 0, 1, 1 };
            var plane = new PresentPlane { Token = 1, ResourceCount = 1, Resources = resources, SourceRect = &rect, DestinationRect = destination };
            uint* parameters = stackalloc uint[8];
            var present = (delegate* unmanaged<nint, uint, PresentPlane*, uint*, int>)XboxDevice.Vtable(queue)[PresentSlot];

            int first = present(queue, 1, &plane, parameters);
            int second = present(queue, 1, &plane, parameters);
            int shutdown = present(queue, 0, null, null);

            Assert.Equal(0, first);
            Assert.Equal(0, second);
            Assert.Equal(0, shutdown);
            Assert.Equal(2, test.Runtime.D3D12X.Presenter.Presents);
        }
        finally
        {
            _ = DestroyWindow(window);
        }
    }

    [Fact]
    public void Frame_event_waits_return_successive_tokens_one_interval_apart()
    {
        using var test = TestRuntime.Create();
        var device = new XboxDevice(test);
        var setInterval = (delegate* unmanaged<nint, nint, uint, uint, uint, int>)XboxDevice.Vtable(device.Device)[SetFrameIntervalSlot];
        var wait = (delegate* unmanaged<nint, uint, uint, nint, uint, ulong*, int>)XboxDevice.Vtable(device.Device)[WaitFrameEventSlot];
        Assert.Equal(0, setInterval(device.Device, 0, 5000, 3, 0));

        ulong current = 0;
        ulong next = 0;
        ulong after = 0;

        // No wait (flags 2, timeout 0) for the current token, then two blocking waits for the next origins.
        Assert.Equal(0, wait(device.Device, 0, 0, 0, 2, &current));
        Assert.Equal(0, wait(device.Device, 0, Infinite, 0, 0, &next));
        long start = Stopwatch.GetTimestamp();
        Assert.Equal(0, wait(device.Device, 0, Infinite, 0, 0, &after));
        var waited = Stopwatch.GetElapsedTime(start);

        Assert.True(current >= 1);
        Assert.Equal(next + 1, after);
        Assert.InRange(waited.TotalMilliseconds, 1, 100);
    }

    private static nint RenderTarget(XboxDevice device, ulong width, uint height)
    {
        int* properties = stackalloc int[] { DefaultHeap, 0, 0, 0, 0 };
        var desc = new ResourceDescription
        {
            Dimension = Texture2D,
            Width = width,
            Height = height,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Rgba8Unorm,
            SampleCount = 1,
            Flags = AllowRenderTarget,
        };
        Guid iid = ResourceIid;
        nint resource = 0;
        var create = (delegate* unmanaged<nint, int*, int, ResourceDescription*, int, void*, Guid*, nint*, int>)XboxDevice.Vtable(device.Device)[CreateCommittedResourceSlot];
        Assert.Equal(0, create(device.Device, properties, 0, &desc, RenderTargetState, null, &iid, &resource));

        return resource;
    }

    private static nint CreateHiddenWindow()
    {
        fixed (char* className = "STATIC")
        {
            nint window = CreateWindowExW(0, className, null, PopupStyle, 0, 0, 64, 32, 0, 0, 0, 0);
            Assert.NotEqual(0, window);

            return window;
        }
    }

    [DllImport("user32")]
    private static extern nint CreateWindowExW(uint exStyle, char* className, char* title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32")]
    private static extern int DestroyWindow(nint window);
}
