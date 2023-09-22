using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cosmic.Core;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;
using static Cosmic.Core.Drawing;
using static Cosmic.Core.DrawLayers;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Input;
using static Cosmic.Core.Palette;
using static Cosmic.Core.Stage;
using static SDL2.SDL;

namespace Cosmic.Graphics;

public sealed partial class Renderer : IDisposable
{
    sealed class GarbageCollectingResourceFactory : ResourceFactory, IDisposable
    {
        readonly ResourceFactory rf;
        readonly Stack<DeviceResource> resources = new();

        public GarbageCollectingResourceFactory(GraphicsDevice gd)
            : base(gd.Features)
        {
            rf = gd.ResourceFactory;
        }

        public override GraphicsBackend BackendType => rf.BackendType;

        TResource AddRef<TResource>(TResource resource)
            where TResource : DeviceResource
        {
            if (!resources.Contains(resource))
            {
                resources.Push(resource);
            }
            return resource;
        }

        public override CommandList CreateCommandList(ref CommandListDescription description)
        {
            return AddRef(rf.CreateCommandList(ref description));
        }

        public override Fence CreateFence(bool signaled)
        {
            return AddRef(rf.CreateFence(signaled));
        }

        public Framebuffer CreateFramebuffer(
            string name,
            Texture target
        )
        {
            var ret = CreateFramebuffer(
                new FramebufferDescription(
                    null,
                    target
                )
            );
            ret.Name = name;
            return AddRef(ret);
        }

        public override Framebuffer CreateFramebuffer(ref FramebufferDescription description)
        {
            return AddRef(rf.CreateFramebuffer(ref description));
        }

        public override ResourceLayout CreateResourceLayout(ref ResourceLayoutDescription description)
        {
            return AddRef(rf.CreateResourceLayout(ref description));
        }

        public ResourceSet CreateResourceSet(
            string name,
            ResourceSetDescription description
        )
        {
            var ret = CreateResourceSet(description);
            ret.Name = name;
            return AddRef(ret);
        }

        public override ResourceSet CreateResourceSet(ref ResourceSetDescription description)
        {
            return AddRef(rf.CreateResourceSet(ref description));
        }

        public override Swapchain CreateSwapchain(ref SwapchainDescription description)
        {
            return AddRef(rf.CreateSwapchain(ref description));
        }

        public DeviceBuffer CreateBuffer(
            string name,
            BufferDescription description
        )
        {
            var ret = CreateBuffer(description);
            ret.Name = name;
            return AddRef(ret);
        }

        protected override DeviceBuffer CreateBufferCore(ref BufferDescription description)
        {
            return AddRef(rf.CreateBuffer(ref description));
        }

        static readonly string shaderPrelude = $@"#version 450
#define SCREEN_XSIZE ({SCREEN_XSIZE})
#define SCREEN_YSIZE ({SCREEN_YSIZE})
#define MaxHwTextureDimension ({Sprite.MaxHwTextureDimension})
#define MaxHwTextures ({Sprite.MaxHwTextures})
#define SCREEN_CENTERX ({SCREEN_CENTERX})
#define TILE_COUNT ({TILE_COUNT})
{string.Concat(Enum.GetNames<DrawBlendMode>().Select((n, i) => $"#define BLEND_{n.ToUpper()} ({i})\n"))}";

        public (Pipeline, ResourceLayout) CreatePipelineEasy(
            string name,
            BlendStateDescription blendStateDescription,
            OutputDescription outputDescription,
            string? shaderNameOverride = null,
            ResourceLayout? resourceLayoutOverride = null
        )
        {
#if DEBUG
            var vertText = shaderPrelude + File.ReadAllText("Cosmic.Graphics/EngineShaders/" + (shaderNameOverride ?? name) + ".vert");
            var fragText = shaderPrelude + File.ReadAllText("Cosmic.Graphics/EngineShaders/" + (shaderNameOverride ?? name) + ".frag");
#else
            var vertText = shaderPrelude + GetEmbeddedResourceText((shaderNameOverride ?? name) + ".vert");
            var fragText = shaderPrelude + GetEmbeddedResourceText((shaderNameOverride ?? name) + ".frag");
#endif

            var (shaders, result) = CreateFromSpirv(
                rf,
                new ShaderDescription(
                    ShaderStages.Vertex,
                    Encoding.ASCII.GetBytes(vertText),
                    "main",
                    true
                ),
                new ShaderDescription(
                    ShaderStages.Fragment,
                    Encoding.ASCII.GetBytes(fragText),
                    "main",
                    true
                ),
                new CrossCompileOptions(
                    true,
                    true,
                    true
                )
            );

            var foo = GLSLhelper.Extract.Uniforms(GLSLhelper.Transformation.RemoveComments(GLSLhelper.Transformation.UnixLineEndings(fragText)));

            var resourceLayout = resourceLayoutOverride ?? AddRef(rf.CreateResourceLayout(result.Reflection.ResourceLayouts[0]));
            var pipeline = CreateGraphicsPipeline(
                new GraphicsPipelineDescription(
                    blendStateDescription,
                    DepthStencilStateDescription.Disabled,
                    new RasterizerStateDescription(
                        FaceCullMode.None,
                        PolygonFillMode.Solid,
                        FrontFace.CounterClockwise,
                        false,
                        true
                    ),
                    PrimitiveTopology.TriangleList,
                    new ShaderSetDescription(
                        new VertexLayoutDescription[]
                        {
                                new VertexLayoutDescription(
                                    result.Reflection.VertexElements
                                )
                        },
                        shaders
                    ),
                    resourceLayout,
                    outputDescription
                )
            );
            return (AddRef(pipeline), resourceLayout);
        }

        (Shader[] shaders, VertexFragmentCompilationResult result) CreateFromSpirv(
            ResourceFactory factory,
            ShaderDescription vertexShaderDescription,
            ShaderDescription fragmentShaderDescription,
            CrossCompileOptions options)
        {
            GraphicsBackend backend = factory.BackendType;
            if (backend == GraphicsBackend.Vulkan)
            {
                vertexShaderDescription.ShaderBytes = EnsureSpirv(vertexShaderDescription);
                fragmentShaderDescription.ShaderBytes = EnsureSpirv(fragmentShaderDescription);

                return (new Shader[]
                {
                        factory.CreateShader(ref vertexShaderDescription),
                        factory.CreateShader(ref fragmentShaderDescription)
                }, null);
            }

            CrossCompileTarget target = GetCompilationTarget(factory.BackendType);
            VertexFragmentCompilationResult compilationResult = SpirvCompilation.CompileVertexFragment(
                vertexShaderDescription.ShaderBytes,
                fragmentShaderDescription.ShaderBytes,
                target,
                options);

            string vertexEntryPoint = (backend == GraphicsBackend.Metal && vertexShaderDescription.EntryPoint == "main")
                ? "main0"
                : vertexShaderDescription.EntryPoint;
            byte[] vertexBytes = GetBytes(backend, compilationResult.VertexShader);
            Shader vertexShader = factory.CreateShader(new ShaderDescription(
                vertexShaderDescription.Stage,
                vertexBytes,
                vertexEntryPoint));

            string fragmentEntryPoint = (backend == GraphicsBackend.Metal && fragmentShaderDescription.EntryPoint == "main")
                ? "main0"
                : fragmentShaderDescription.EntryPoint;
            byte[] fragmentBytes = GetBytes(backend, compilationResult.FragmentShader);
            Shader fragmentShader = factory.CreateShader(new ShaderDescription(
                fragmentShaderDescription.Stage,
                fragmentBytes,
                fragmentEntryPoint));

            return (new Shader[] { AddRef(vertexShader), AddRef(fragmentShader) }, compilationResult);
        }

        static CrossCompileTarget GetCompilationTarget(GraphicsBackend backend)
        {
            return backend switch
            {
                GraphicsBackend.Direct3D11 => CrossCompileTarget.HLSL,
                GraphicsBackend.OpenGL => CrossCompileTarget.GLSL,
                GraphicsBackend.Metal => CrossCompileTarget.MSL,
                GraphicsBackend.OpenGLES => CrossCompileTarget.ESSL,
                _ => throw new SpirvCompilationException($"Invalid GraphicsBackend: {backend}"),
            };
        }

        static bool HasSpirvHeader(byte[] bytes)
        {
            return bytes.Length > 4
                && bytes[0] == 0x03
                && bytes[1] == 0x02
                && bytes[2] == 0x23
                && bytes[3] == 0x07;
        }

        static unsafe byte[] EnsureSpirv(ShaderDescription description)
        {
            if (HasSpirvHeader(description.ShaderBytes))
            {
                return description.ShaderBytes;
            }
            else
            {
                SpirvCompilationResult glslCompileResult = SpirvCompilation.CompileGlslToSpirv(
                    Encoding.ASCII.GetString(description.ShaderBytes),
                    null,
                    description.Stage,
                    new GlslCompileOptions(
                        description.Debug
                    )
                );
                return glslCompileResult.SpirvBytes;
            }
        }

        static byte[] GetBytes(GraphicsBackend backend, string code)
        {
            return backend switch
            {
                GraphicsBackend.Direct3D11 or GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES => Encoding.ASCII.GetBytes(code),
                GraphicsBackend.Metal => Encoding.UTF8.GetBytes(code),
                _ => throw new SpirvCompilationException($"Invalid GraphicsBackend: {backend}"),
            };
        }

        protected override Pipeline CreateGraphicsPipelineCore(ref GraphicsPipelineDescription description)
        {
            return AddRef(rf.CreateGraphicsPipeline(ref description));
        }

        public Sampler CreateSampler(
            string name,
            SamplerAddressMode uvAddressMode,
            SamplerFilter uvFilter
        )
        {
            var ret = rf.CreateSampler(new SamplerDescription(
                uvAddressMode,
                uvAddressMode,
                SamplerAddressMode.Clamp,
                uvFilter,
                null,
                0,
                0,
                0,
                0,
                SamplerBorderColor.OpaqueBlack
            ));
            ret.Name = name;
            return AddRef(ret);
        }

        protected override Sampler CreateSamplerCore(ref SamplerDescription description)
        {
            return AddRef(rf.CreateSampler(ref description));
        }

        protected override Shader CreateShaderCore(ref ShaderDescription description)
        {
            return AddRef(rf.CreateShader(ref description));
        }

        public (Texture, TextureView) CreateTexture1D(
            string name,
            uint length,
            PixelFormat pixelFormat,
            TextureUsage usage
        )
        {
            var tex = rf.CreateTexture(TextureDescription.Texture1D(length, 1, 1, pixelFormat, usage));
            tex.Name = name;
            var view = rf.CreateTextureView(tex);
            view.Name = name + "_view";
            return (AddRef(tex), AddRef(view));
        }

        public (Texture, TextureView) CreateTexture2D(
            string name,
            uint width,
            uint height,
            PixelFormat pixelFormat,
            TextureUsage usage
        )
        {
            var tex = rf.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1, pixelFormat, usage));
            tex.Name = name;
            var view = rf.CreateTextureView(tex);
            view.Name = name + "_view";
            return (AddRef(tex), AddRef(view));
        }

        public (Texture, TextureView) CreateTexture3D(
            string name,
            uint width,
            uint height,
            uint depth,
            PixelFormat pixelFormat,
            TextureUsage usage
        )
        {
            var tex = rf.CreateTexture(TextureDescription.Texture3D(width, height, depth, 1, pixelFormat, usage));
            tex.Name = name;
            var view = rf.CreateTextureView(tex);
            view.Name = name + "_view";
            return (AddRef(tex), AddRef(view));
        }

        protected override Texture CreateTextureCore(ulong nativeTexture, ref TextureDescription description)
        {
            return AddRef(rf.CreateTexture(nativeTexture, ref description));
        }

        protected override Texture CreateTextureCore(ref TextureDescription description)
        {
            return AddRef(rf.CreateTexture(ref description));
        }

        protected override TextureView CreateTextureViewCore(ref TextureViewDescription description)
        {
            return AddRef(rf.CreateTextureView(ref description));
        }

        public void ClearResources()
        {
            while (resources.TryPop(out var result))
            {
                if (result is IDisposable id)
                {
                    id?.Dispose();
                }
            }
            resources.Clear();
        }

        public void Dispose()
        {
            ClearResources();
        }

        public override Pipeline CreateComputePipeline(ref ComputePipelineDescription description)
        {
            throw new NotImplementedException();
        }
    }

    readonly Vector4[] quadVerts = new Vector4[]
    {
        new Vector4(-1, 1, 0, 1), // 0
        new Vector4(1, 1, 1, 1), // 1
        new Vector4(-1, -1, 0, 0), // 2
        new Vector4(1, -1, 1, 0), // 3
    };

    public void ResetSceneResources()
    {
        sceneResourceFactory.ClearResources();
        Task.WaitAll(
            Task.Run(() =>
            {
                (drawListPipelineAlpha, resourceLayoutDrawList) = sceneResourceFactory.CreatePipelineEasy(
                    nameof(drawListPipelineAlpha),
                    GraphicsResourceDescriptions.blendStateDescriptionAlpha,
                    gameViewFramebuffer.OutputDescription,
                    "drawlist",
                    resourceLayoutDrawList
                );
            }),
            Task.Run(() =>
            {
                (drawListPipelineAdditive, resourceLayoutDrawList) = sceneResourceFactory.CreatePipelineEasy(
                    nameof(drawListPipelineAdditive),
                    GraphicsResourceDescriptions.blendStateDescriptionAdditive,
                    gameViewFramebuffer.OutputDescription,
                    "drawlist",
                    resourceLayoutDrawList
                );
            }),
            Task.Run(() =>
            {
                (drawListPipelineSubtractive, resourceLayoutDrawList) = sceneResourceFactory.CreatePipelineEasy(
                    nameof(drawListPipelineSubtractive),
                    GraphicsResourceDescriptions.blendStateDescriptionSubtractive,
                    gameViewFramebuffer.OutputDescription,
                    "drawlist",
                    resourceLayoutDrawList
                );
            })
        );
        foreach (var value in Enum.GetValues<DrawBlendMode>())
        {
            drawListPipelines[value] = value switch
            {
                DrawBlendMode.Additive => drawListPipelineAdditive,
                DrawBlendMode.Subtractive => drawListPipelineSubtractive,
                _ => drawListPipelineAlpha
            };
        }
    }

    void WindowResizedCallback()
    {
        var winSize = GetWindowSize();
        SetScreenDimensions(winSize.w, winSize.h);
    }

    void WindowClosingCallback()
    {
        gameMode = EngineStates.ENGINE_EXITGAME;
    }

    void WindowClosedCallback()
    {
        gameMode = EngineStates.ENGINE_EXITGAME;
    }

    void WindowFocusGainedCallback()
    {
        Engine.hasFocus = true;
    }

    readonly ICosmicPlatform platform;
    readonly Sdl2Window Enginewindow;
    ImGuiRenderer imguiRenderer;
    ImFontPtr menuFontPtr;

    readonly GraphicsDevice graphicsDevice;
    readonly GarbageCollectingResourceFactory mainResourceFactory, sceneResourceFactory;
    CommandList commandList;
    Pipeline pipelineVideo, imguiPipeline;
    ResourceLayout resourceLayoutWindow, resourceLayoutVideo;
    readonly Dictionary<DrawBlendMode, Pipeline> drawListPipelines = new();
    Pipeline drawListPipelineAlpha, drawListPipelineAdditive, drawListPipelineSubtractive;
    ResourceLayout resourceLayoutDrawList;
    Framebuffer gameViewFramebuffer;
    Texture gameViewFbColorTarget;
    TextureView gameViewFbColorTargetView;
    Texture gameViewFbColorTargetPrev;
    TextureView gameViewFbColorTargetPrevView;
    Texture gfxBufferTexture;
    TextureView gfxBufferTextureView;
    Texture paletteBufferTexture;
    TextureView paletteBufferTextureView;
    Texture paletteIndicesTexture;
    TextureView paletteIndicesTextureView;
    Texture tilesetTexture;
    TextureView tilesetTextureView;
    Texture tilesetCollisionHighTexture;
    TextureView tilesetCollisionHighTextureView;
    Texture tilesetCollisionLowTexture;
    TextureView tilesetCollisionLowTextureView;
    Texture floor3dTexture;
    TextureView floor3dTextureView;
    Texture videoTextureY, videoTextureU, videoTextureV;
    TextureView videoTextureYView, videoTextureUView, videoTextureVView;
    Sampler screenBufferSampler, imGuiSampler;
    DeviceBuffer vertexBuffer, vertexBufferDrawList, indexBufferDrawList, drawListVarsBuffer;
    ResourceSet resourceSetDrawList, resourceSetDrawListColLow, resourceSetVideo, resourceSetImgui;
    private readonly uint[] indexBufferManaged = new uint[0x60000 + 2 /* 262144 + 2 */];

    public uint BaseRenderScale { get; private set; } = 1;
    public bool VSync
    {
        get => graphicsDevice.SyncToVerticalBlank;
        set => graphicsDevice.SyncToVerticalBlank = value;
    }
    public byte FastForwardSpeed { get; set; } = 8;

    public delegate void OnFocusLostCallback();
    public event OnFocusLostCallback OnFocusLost;

    public (int w, int h) GetWindowSize()
    {
        return (Enginewindow.Width, Enginewindow.Height);
    }

    void WindowKeyDownCallback(KeyEvent key)
    {
        switch (key.Key)
        {
            default: break;

            case Key.F10:
                if (platform.gameDebugMode)
                    Engine.showPaletteOverlay = !Engine.showPaletteOverlay;
                break;

            case Key.BackSpace:
                if (platform.gameDebugMode)
                    platform.SetGameSpeed(FastForwardSpeed);
                break;

            case Key.F16:
                if (OperatingSystem.IsMacOS())
                {
                    if (platform.gameDebugMode && platform.enginePaused)
                        Engine.RunFrame();
                }
                break;

            case Key.F5:
                if (platform.gameDebugMode)
                {
                    ResetSceneResources();
                    Stage.currentStageFolder = string.Empty;
                    Stage.stageMode = Stage.StageModes.STAGEMODE_LOAD;
                }
                break;

            case Key.F7:
                if (OperatingSystem.IsMacOS())
                {
                    if (platform.gameDebugMode)
                    {
                        platform.enginePaused = !platform.enginePaused;
                    }
                }
                break;

            case Key.F11:
            case Key.Insert:
                if (!OperatingSystem.IsMacOS())
                {
                    if (platform.gameDebugMode && platform.enginePaused)
                    {
                        Engine.RunFrame();
                    }
                }
                break;

            case Key.F12:
            case Key.Pause:
                if (!OperatingSystem.IsMacOS())
                {
                    if (platform.gameDebugMode)
                    {
                        platform.enginePaused = !platform.enginePaused;
                    }
                }
                break;
        }
    }

    void WindowKeyUpCallback(KeyEvent key)
    {
        if (key.Key == Key.BackSpace)
        {
            platform.SetGameSpeed(1);
        }
    }

    void WindowMouseUpCallback(MouseEvent mouseEvent)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }
        if (touches <= 1)
        { // Touch always takes priority over mouse
            switch (mouseEvent.MouseButton)
            {
                case MouseButton.Left: touchDown[0] = 0; break;
            }
            touches = 0;
        }
    }

    void WindowMouseDownCallback(MouseEvent mouseEvent)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }
        if (touches <= 1)
        { // Touch always takes priority over mouse
            switch (mouseEvent.MouseButton)
            {
                case MouseButton.Left: touchDown[0] = 1; break;
            }
            touches = 1;
        }
    }

    void WindowMouseMoveCallback(MouseMoveEventArgs moveArgs)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }
        if (touches <= 1)
        { // Touch always takes priority over mouse
            uint state = SDL2.SDL.SDL_GetMouseState(out touchX[0], out touchY[0]);

            SDL2.SDL.SDL_GetWindowSize(Enginewindow.SdlWindowHandle, out var width, out var height);
            touchX[0] = (int)((touchX[0] / (float)width) * SCREEN_XSIZE);
            touchY[0] = (int)((touchY[0] / (float)height) * SCREEN_YSIZE);
        }
    }

    void SetupWindow()
    {
        Enginewindow.Resizable = true;
        Enginewindow.Resized += WindowResizedCallback;
        Enginewindow.Closing += WindowClosingCallback;
        Enginewindow.Closed += WindowClosedCallback;
        OnFocusLost += platform.OnFocusLost;
        Enginewindow.FocusLost += () => { OnFocusLost?.Invoke(); };
        Enginewindow.FocusGained += WindowFocusGainedCallback;
        Enginewindow.KeyDown += WindowKeyDownCallback;
        Enginewindow.KeyUp += WindowKeyUpCallback;
        Enginewindow.MouseUp += WindowMouseUpCallback;
        Enginewindow.MouseDown += WindowMouseDownCallback;
        Enginewindow.MouseMove += WindowMouseMoveCallback;
    }

    static class GraphicsResourceDescriptions
    {
        public static readonly BlendStateDescription blendStateDescriptionAlpha = new(
            RgbaFloat.White,
            new BlendAttachmentDescription(
                true, BlendFactor.SourceAlpha,
                BlendFactor.InverseSourceAlpha,
                BlendFunction.Add,
                BlendFactor.One,
                BlendFactor.One,
                BlendFunction.Maximum
            )
        );

        public static readonly BlendStateDescription blendStateDescriptionAdditive = new(
            RgbaFloat.White,
            new BlendAttachmentDescription(
                true, BlendFactor.SourceAlpha,
                BlendFactor.One,
                BlendFunction.Add,
                BlendFactor.One,
                BlendFactor.One,
                BlendFunction.Maximum
            )
        );

        public static readonly BlendStateDescription blendStateDescriptionSubtractive = new(
            RgbaFloat.White,
            new BlendAttachmentDescription(
                true, BlendFactor.SourceAlpha,
                BlendFactor.One,
                BlendFunction.Subtract,
                BlendFactor.One,
                BlendFactor.One,
                BlendFunction.Maximum
            )
        );
    }

    public void SetupVideoResources(VideoInfo videoInfo)
    {
        (videoTextureY, videoTextureYView) = mainResourceFactory.CreateTexture2D(
            nameof(videoTextureY),
            (uint)videoInfo.yWidth,
            (uint)videoInfo.yHeight,
            Veldrid.PixelFormat.R8_UNorm,
            Veldrid.TextureUsage.Sampled
        );

        (videoTextureU, videoTextureUView) = mainResourceFactory.CreateTexture2D(
            nameof(videoTextureU),
            (uint)videoInfo.uvWidth,
            (uint)videoInfo.uvHeight,
            Veldrid.PixelFormat.R8_UNorm,
            Veldrid.TextureUsage.Sampled
        );

        (videoTextureV, videoTextureVView) = mainResourceFactory.CreateTexture2D(
            nameof(videoTextureV),
            (uint)videoInfo.uvWidth,
            (uint)videoInfo.uvHeight,
            Veldrid.PixelFormat.R8_UNorm,
            Veldrid.TextureUsage.Sampled
        );

        (pipelineVideo, resourceLayoutVideo) = mainResourceFactory.CreatePipelineEasy("video", BlendStateDescription.SingleAlphaBlend, gameViewFramebuffer.OutputDescription);

        resourceSetVideo = mainResourceFactory.CreateResourceSet(
            nameof(resourceSetVideo),
            new Veldrid.ResourceSetDescription(
                resourceLayoutVideo,
                videoTextureYView,
                videoTextureUView,
                videoTextureVView,
                screenBufferSampler
            )
        );
    }

    public void UpdateVideoResources(nint y, nint u, nint v, VideoInfo videoInfo)
    {
        graphicsDevice.UpdateTexture(videoTextureY, y, (uint)(videoInfo.yWidth * videoInfo.yHeight), 0, 0, 0, (uint)videoInfo.yWidth, (uint)videoInfo.yHeight, 1, 0, 0);
        graphicsDevice.UpdateTexture(videoTextureU, u, (uint)(videoInfo.uvWidth * videoInfo.uvHeight), 0, 0, 0, (uint)videoInfo.uvWidth, (uint)videoInfo.uvHeight, 1, 0, 0);
        graphicsDevice.UpdateTexture(videoTextureV, v, (uint)(videoInfo.uvWidth * videoInfo.uvHeight), 0, 0, 0, (uint)videoInfo.uvWidth, (uint)videoInfo.uvHeight, 1, 0, 0);
    }

    public void FreeVideoResources()
    {
        videoTextureYView?.Dispose();
        videoTextureY?.Dispose();
        resourceSetVideo?.Dispose();
    }

    void SetupRenderResources()
    {
        var resourceCreationTasks = new ConcurrentBag<Task>();
        void AddTask(Action creation)
        {
            resourceCreationTasks.Add(Task.Run(creation));
        }
        void FinishTasksAndReset()
        {
            foreach (var task in resourceCreationTasks)
            {
                task.Wait();
            }
            resourceCreationTasks.Clear();
        }

        commandList = mainResourceFactory.CreateCommandList();

        AddTask(() =>
        {
            (gameViewFbColorTarget, gameViewFbColorTargetView) = mainResourceFactory.CreateTexture2D(nameof(gameViewFbColorTarget), SCREEN_XSIZE * 2, SCREEN_YSIZE * 2, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Sampled | TextureUsage.RenderTarget);
        });
        AddTask(() =>
        {
            (gameViewFbColorTargetPrev, gameViewFbColorTargetPrevView) = mainResourceFactory.CreateTexture2D(nameof(gameViewFbColorTargetPrev), SCREEN_XSIZE * 2, SCREEN_YSIZE * 2, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (gfxBufferTexture, gfxBufferTextureView) = mainResourceFactory.CreateTexture3D(nameof(gfxBufferTexture), Sprite.MaxHwTextureDimension, Sprite.MaxHwTextureDimension, Sprite.MaxHwTextures, PixelFormat.R8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (paletteIndicesTexture, paletteIndicesTextureView) = mainResourceFactory.CreateTexture1D(nameof(paletteIndicesTexture), SCREEN_YSIZE, PixelFormat.R8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (paletteBufferTexture, paletteBufferTextureView) = mainResourceFactory.CreateTexture2D(nameof(paletteBufferTexture), PALETTE_SIZE, PALETTE_COUNT, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (tilesetTexture, tilesetTextureView) = mainResourceFactory.CreateTexture3D(nameof(tilesetTexture), TILE_SIZE, TILE_SIZE, TILE_COUNT, PixelFormat.R8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (tilesetCollisionHighTexture, tilesetCollisionHighTextureView) = mainResourceFactory.CreateTexture3D(nameof(tilesetCollisionHighTexture), TILE_SIZE, TILE_SIZE, TILE_COUNT, PixelFormat.R8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (tilesetCollisionLowTexture, tilesetCollisionLowTextureView) = mainResourceFactory.CreateTexture3D(nameof(tilesetCollisionLowTexture), TILE_SIZE, TILE_SIZE, TILE_COUNT, PixelFormat.R8_UNorm, TextureUsage.Sampled);
        });
        AddTask(() =>
        {
            (floor3dTexture, floor3dTextureView) = mainResourceFactory.CreateTexture2D(nameof(floor3dTexture), 256, 256, PixelFormat.R16_UNorm, TextureUsage.Sampled);
        });

        AddTask(() =>
        {
            drawListVarsBuffer = mainResourceFactory.CreateBuffer(nameof(drawListVarsBuffer), new BufferDescription(32, BufferUsage.UniformBuffer));
        });

        FinishTasksAndReset();

        gameViewFramebuffer = mainResourceFactory.CreateFramebuffer(
            nameof(gameViewFramebuffer),
            gameViewFbColorTarget
        );

        AddTask(() =>
        {
            (_, resourceLayoutWindow) = mainResourceFactory.CreatePipelineEasy("main", BlendStateDescription.SingleAlphaBlend, gameViewFramebuffer.OutputDescription);
        });

        AddTask(ResetSceneResources);

        AddTask(() =>
        {
            (imguiPipeline, _) = mainResourceFactory.CreatePipelineEasy(
                "imgui",
                BlendStateDescription.SingleAlphaBlend,
                graphicsDevice.SwapchainFramebuffer.OutputDescription,
                "main"
            );
        });

        AddTask(() =>
        {
            vertexBuffer = mainResourceFactory.CreateBuffer(nameof(vertexBuffer), new BufferDescription(96, BufferUsage.VertexBuffer));
            graphicsDevice.UpdateBuffer(vertexBuffer, 0, quadVerts);
        });

        AddTask(() =>
        {
            vertexBufferDrawList = mainResourceFactory.CreateBuffer(nameof(vertexBufferDrawList), new BufferDescription(DrawVertexFace.SizeInBytes * MaxDrawFaces, BufferUsage.VertexBuffer));
        });

        AddTask(() =>
        {
            indexBufferDrawList = mainResourceFactory.CreateBuffer(nameof(indexBufferDrawList), new BufferDescription(0x60002 * sizeof(uint), BufferUsage.IndexBuffer));

            var face = 0;
            for (var i = 0; i < indexBufferManaged.Length / 6; i++)
            {
                indexBufferManaged[face++] = (uint)((i * 4) + 0);
                indexBufferManaged[face++] = (uint)((i * 4) + 1);
                indexBufferManaged[face++] = (uint)((i * 4) + 2);
                indexBufferManaged[face++] = (uint)((i * 4) + 2);
                indexBufferManaged[face++] = (uint)((i * 4) + 1);
                indexBufferManaged[face++] = (uint)((i * 4) + 3);
            }
            graphicsDevice.UpdateBuffer(indexBufferDrawList, 0, indexBufferManaged);
        });

        AddTask(() =>
        {
            screenBufferSampler = mainResourceFactory.CreateSampler(
                nameof(screenBufferSampler),
                SamplerAddressMode.Wrap,
                SamplerFilter.MinPoint_MagPoint_MipPoint
            );
        });

        AddTask(() =>
        {
            imGuiSampler = mainResourceFactory.CreateSampler(
                nameof(imGuiSampler),
                SamplerAddressMode.Clamp,
                SamplerFilter.MinLinear_MagLinear_MipLinear
            );
        });

        FinishTasksAndReset();

        resourceSetDrawList = mainResourceFactory.CreateResourceSet(
            nameof(resourceSetDrawList),
            new ResourceSetDescription(
                resourceLayoutDrawList,
                gameViewFbColorTargetPrevView,
                gfxBufferTextureView,
                paletteBufferTextureView,
                paletteIndicesTextureView,
                tilesetTextureView,
                floor3dTextureView,
                screenBufferSampler,
                drawListVarsBuffer
            )
        );

        resourceSetDrawListColLow = mainResourceFactory.CreateResourceSet(
            nameof(resourceSetDrawList),
            new ResourceSetDescription(
                resourceLayoutDrawList,
                gameViewFbColorTargetPrevView,
                gfxBufferTextureView,
                paletteBufferTextureView,
                paletteIndicesTextureView,
                tilesetCollisionLowTextureView,
                floor3dTextureView,
                screenBufferSampler,
                drawListVarsBuffer
            )
        );

        resourceSetImgui = mainResourceFactory.CreateResourceSet(
            nameof(resourceSetImgui),
            new ResourceSetDescription(
                resourceLayoutWindow,
                gameViewFbColorTargetView,
                imGuiSampler
            )
        );

        imguiRenderer = new(graphicsDevice, graphicsDevice.SwapchainFramebuffer.OutputDescription, Enginewindow.Width, Enginewindow.Height);
        unsafe
        {
            var builderPtr = ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder();
            var builderPtrManaged = new ImFontGlyphRangesBuilderPtr(builderPtr);
            builderPtrManaged.AddRanges(ImGui.GetIO().Fonts.GetGlyphRangesDefault());
            builderPtrManaged.AddChar('←');
            builderPtrManaged.AddChar('↑');
            builderPtrManaged.AddChar('↓');
            builderPtrManaged.AddChar('→');
            builderPtrManaged.BuildRanges(out var ranges);
            var fontBytes = GetEmbeddedResource("menufont.ttf");
            fixed (byte* fontBytesPtr = fontBytes)
            {
                menuFontPtr = ImGui.GetIO().Fonts.AddFontFromMemoryTTF((nint)fontBytesPtr, 24, 24, 0, ranges.Data);
            }
            ImGui.GetIO().Fonts.Build();
            imguiRenderer.RecreateFontDeviceTexture();
            ImGuiNative.ImFontGlyphRangesBuilder_destroy(builderPtr);
        }
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.HasGamepad;

        FinishTasksAndReset();
    }

    public Renderer(ICosmicPlatform platform, bool borderless)
    {
        this.platform = platform;

        VeldridStartup.CreateWindowAndGraphicsDevice(
        new WindowCreateInfo(
            0,
            0,
            0,
            0,
            WindowState.Normal,
            gameWindowText
        ),
        new GraphicsDeviceOptions(
#if DEBUG
            true,
#else
                false,
#endif
            null,
            false,
            ResourceBindingModel.Improved,
            true,
            true,
            false
        ),
        // GraphicsBackend.Vulkan,
        out Enginewindow,
        out graphicsDevice
    );
        Enginewindow.Visible = false;
        mainResourceFactory = new GarbageCollectingResourceFactory(graphicsDevice);
        sceneResourceFactory = new GarbageCollectingResourceFactory(graphicsDevice);

        SetupWindow();
        SetupRenderResources();

        var menuBarHeight = 0;
        {
            ImGui.PushFont(menuFontPtr);
            if (ImGui.BeginMainMenuBar())
            {
                menuBarHeight = (int)ImGui.GetWindowSize().Y;

                ImGui.EndMainMenuBar();
            }
            ImGui.PopFont();
        }
        _ = SDL_GetDisplayUsableBounds(0, out var safeRect);
        var winSize = (x: SCREEN_XSIZE * BaseRenderScale, y: SCREEN_YSIZE * BaseRenderScale + menuBarHeight);
        Enginewindow.Width = (int)winSize.x;
        Enginewindow.Height = (int)winSize.y;
        SDL_SetWindowPosition(Enginewindow.SdlWindowHandle, safeRect.w / 2 - Enginewindow.Width / 2, safeRect.h / 2 - Enginewindow.Height / 2);

        Enginewindow.BorderVisible = !borderless;

        Enginewindow.Visible = true;
    }

    public void FlipScreen()
    {
        if (gameMode == EngineStates.ENGINE_VIDEOWAIT)
        {
            commandList.Begin();
            commandList.SetPipeline(pipelineVideo);
            commandList.SetGraphicsResourceSet(0, resourceSetVideo);
            commandList.SetFramebuffer(gameViewFramebuffer);
            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBufferDrawList, IndexFormat.UInt32);
            commandList.DrawIndexed(6);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            graphicsDevice.WaitForIdle();
        }

        commandList.Begin();
        commandList.SetPipeline(imguiPipeline);
        commandList.SetGraphicsResourceSet(0, resourceSetImgui);
        commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
        commandList.ClearColorTarget(0, RgbaFloat.Black);
        imguiRenderer.Render(graphicsDevice, commandList);
        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
        graphicsDevice.WaitForIdle();
        graphicsDevice.SwapBuffers();
    }

    public void SetScreenDimensions(int winWidth, int winHeight)
    {
        Enginewindow.Width = winWidth;
        Enginewindow.Height = winHeight;
        graphicsDevice.MainSwapchain.Resize((uint)winWidth, (uint)winHeight);
        imguiRenderer.WindowResized(winWidth, winHeight);
    }

    public void UpdatePrevFramebuffer()
    {
        graphicsDevice.WaitForIdle();
        commandList.Begin();
        commandList.CopyTexture(gameViewFbColorTarget, gameViewFbColorTargetPrev);
        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
    }

    public void ClearDrawLists()
    {
        commandList.Begin();
        commandList.SetFramebuffer(gameViewFramebuffer);
        commandList.ClearColorTarget(0, RgbaFloat.Black);
        if (gameViewFramebuffer.DepthTarget != null)
        {
            commandList.ClearDepthStencil(0);
        }
        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
        graphicsDevice.WaitForIdle();
    }

    public void UpdateHWSurface(int index, int x, int y, int w, int h, byte[] data)
    {
        graphicsDevice.UpdateTexture(gfxBufferTexture, data, (uint)x, (uint)y, (uint)index, (uint)w, (uint)h, 1, 0, 0);
    }

    public unsafe void SubmitDrawList(bool showHitboxes, uint drawFacesCount, DrawVertexFace[] drawFacesList, DrawBlendMode blendMode)
    {
        graphicsDevice.WaitForIdle();
        fixed (DrawVertexFace* drawVertListPtr = drawFacesList)
        {
            graphicsDevice.UpdateBuffer(vertexBufferDrawList, 0, (nint)drawVertListPtr, drawFacesCount * DrawVertexFace.SizeInBytes);
        }
        graphicsDevice.UpdateBuffer(drawListVarsBuffer, 0, new int[] {
                (int)blendMode,
                (int)BaseRenderScale,
                Floor3DX,
                Floor3DY,
                Floor3DZ,
                Floor3DAngle
            });
        commandList.Begin();
        commandList.SetPipeline(drawListPipelines[blendMode]);
        if (showHitboxes)
        {
            commandList.SetGraphicsResourceSet(0, resourceSetDrawListColLow);
        }
        else
        {
            commandList.SetGraphicsResourceSet(0, resourceSetDrawList);
        }
        commandList.SetFramebuffer(gameViewFramebuffer);
        {
            uint viewportStartX = 0;
            uint viewportStartY = 0;
            uint viewportWidth = SCREEN_XSIZE;
            uint viewportHeight = SCREEN_YSIZE;
            uint clipStartX = viewportStartX;
            uint clipStartY = viewportStartY;
            uint clipWidth = viewportWidth;
            uint clipHeight = viewportHeight;
            viewportStartX *= BaseRenderScale;
            viewportStartY *= BaseRenderScale;
            viewportWidth *= BaseRenderScale;
            viewportHeight *= BaseRenderScale;
            clipStartX *= BaseRenderScale;
            clipStartY *= BaseRenderScale;
            clipWidth *= BaseRenderScale;
            clipHeight *= BaseRenderScale;
            commandList.SetViewport(0, new Viewport(viewportStartX, viewportStartY, viewportWidth, viewportHeight, 0, 1));
            commandList.SetScissorRect(0, clipStartX, clipStartY, clipWidth, clipHeight);
        }
        commandList.SetVertexBuffer(0, vertexBufferDrawList);
        commandList.SetIndexBuffer(indexBufferDrawList, IndexFormat.UInt32);
        commandList.SetFramebuffer(gameViewFramebuffer);
        commandList.DrawIndexed(drawFacesCount * 6);
        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
    }

    public void UpdateFloor3DTiledataTexture()
    {
        graphicsDevice.UpdateTexture(floor3dTexture, Stage.tile3DFloorData, 0, 0, 0, floor3dTexture.Width, floor3dTexture.Height, 1, 0, 0);
    }

    public void UpdateTilesetTexture()
    {
        graphicsDevice.UpdateTexture(tilesetTexture, tilesetGFXData, 0, 0, 0, TILE_SIZE, TILE_SIZE, TILE_COUNT, 0, 0);
    }

    public void UpdateTileColLowTexture(byte[] colTextureManaged)
    {
        graphicsDevice.UpdateTexture(tilesetCollisionLowTexture, colTextureManaged, 0, 0, 0, TILE_SIZE, TILE_SIZE, TILE_COUNT, 0, 0);
    }

    public void UpdateTileColHighTexture(byte[] colTextureManaged)
    {
        graphicsDevice.UpdateTexture(tilesetCollisionHighTexture, colTextureManaged, 0, 0, 0, TILE_SIZE, TILE_SIZE, TILE_COUNT, 0, 0);
    }

    public void UpdatePaletteBufferTexture(int[] paletteLineConverted)
    {
        graphicsDevice.UpdateTexture(paletteBufferTexture, paletteLineConverted, 0, 0, 0, PALETTE_SIZE, PALETTE_COUNT, 1, 0, 0);
    }

    public void UpdatePaletteIndicesTexture()
    {
        graphicsDevice.UpdateTexture(paletteIndicesTexture, gfxLineBuffer, 0, 0, 0, (uint)gfxLineBuffer.Length, 0, 0, 0, 0);
    }

    public void UpdateGUI(float deltaTime, ref bool inputDisplay)
    {
        imguiRenderer.Update(deltaTime, Enginewindow.PumpEvents());
        ImGui.PushFont(menuFontPtr);
        DoGUI(ref inputDisplay);
        ImGui.PopFont();
    }

    public nint GetImGuiGameViewTexture()
    {
        return imguiRenderer.GetOrCreateImGuiBinding(graphicsDevice.ResourceFactory, gameViewFramebuffer.ColorTargets[0].Target);
    }

    public (uint w, uint h) GetMainFramebufferSize()
    {
        return (graphicsDevice.SwapchainFramebuffer.Width, graphicsDevice.SwapchainFramebuffer.Height);
    }

    public void CloseEngineWindow()
    {
        Enginewindow?.Close();
    }

    public void Dispose()
    {
        imguiRenderer?.Dispose();

        commandList?.Dispose();
        graphicsDevice?.Dispose();

        sceneResourceFactory?.Dispose();
        mainResourceFactory?.Dispose();
        Enginewindow?.Close();
    }

    internal static byte[] GetEmbeddedResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        var ret = new byte[stream!.Length];
        stream.Read(ret, 0, ret.Length);
        return ret;
    }

    internal static string GetEmbeddedResourceText(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}