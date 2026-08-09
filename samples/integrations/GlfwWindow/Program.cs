using NeoVeldrid;
using NeoVeldrid.OpenGL;
using NeoVeldrid.SPIRV;
using Silk.NET.GLFW;
using System;
using System.Numerics;
using System.Text;

namespace GlfwWindow;

internal unsafe class Program
{
    private static Glfw _glfw;
    private static WindowHandle* _hwnd;
    private static GraphicsDevice _graphicsDevice;

    private static CommandList _commandList;
    private static DeviceBuffer _vertexBuffer;
    private static DeviceBuffer _indexBuffer;
    private static Shader[] _shaders;
    private static Pipeline _pipeline;

    private const string VertexCode = @"
#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}";

    private const string FragmentCode = @"
#version 450

layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

    static void Main(string[] args)
    {
        string backendEnv = Environment.GetEnvironmentVariable("NEOVELDRID_BACKEND");
        GraphicsBackend backend = string.IsNullOrEmpty(backendEnv)
            ? GraphicsDevice.GetPlatformDefaultBackend()
            : backendEnv.ToLowerInvariant() switch
            {
                "d3d11" or "direct3d11" => GraphicsBackend.Direct3D11,
                "vulkan" or "vk" => GraphicsBackend.Vulkan,
                "opengl" or "gl" => GraphicsBackend.OpenGL,
                "opengles" or "gles" => GraphicsBackend.OpenGLES,
                _ => throw new InvalidOperationException($"Unknown NEOVELDRID_BACKEND: '{backendEnv}'")
            };

        // Populate bindings and initialize GLFW.
        _glfw = Glfw.GetApi();
        if (!_glfw.Init())
            throw new InvalidOperationException("Failed to initialize GLFW.");

        // GLFW handles the OpenGL context itself, but if we use another backend we need
        // to tell GLFW not to create the OpenGL context.
        if (backend == GraphicsBackend.OpenGL || backend == GraphicsBackend.OpenGLES)
        {
            GraphicsApiVersion version = GraphicsDevice.GetBackendVersion(backend);

            _glfw.WindowHint(WindowHintClientApi.ClientApi,
                backend == GraphicsBackend.OpenGL
                ? ClientApi.OpenGL
                : ClientApi.OpenGLES);

            // Ask for the newest version the driver reports.
            _glfw.WindowHint(WindowHintInt.ContextVersionMajor, version.Major);
            _glfw.WindowHint(WindowHintInt.ContextVersionMinor, version.Minor);

            // Disables the default 24-bit depth buffer.
            _glfw.WindowHint(WindowHintInt.DepthBits, 0);

            // Only desktop OpenGL needs a core profile.
            if (backend == GraphicsBackend.OpenGL)
                _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
        }
        else
        {
            _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
        }

        int width = 960;
        int height = 540;
        _hwnd = _glfw.CreateWindow(width, height, $"GLFW + NeoVeldrid ({backend})", null, null);
        if (_hwnd == null)
        {
            _glfw.Terminate(); // We can't continue from this point on, so we need to dispose of GLFW.
            throw new InvalidOperationException("Failed to create GLFW window.");
        }

        // Create the GraphicsDevice for the specified backend and attach it to the GLFW window.
        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };
        CreateGraphicsDevice(backend, options, width, height);

        // Create the resources needed for drawing.
        CreateResources();

        while (!_glfw.WindowShouldClose(_hwnd))
        {
            // Without polling for events, titlebars and input won't be checked.
            _glfw.PollEvents();

            // We need to sync the GLFW window size and NeoVeldrid's framebuffer, or window resizing
            // will not work.
            _glfw.GetFramebufferSize(_hwnd, out int fbWidth, out int fbHeight);

            if (fbWidth > 0 && fbHeight > 0 &&
                (fbWidth != _graphicsDevice.SwapchainFramebuffer.Width
                || fbHeight != _graphicsDevice.SwapchainFramebuffer.Height))
            {
                _graphicsDevice.ResizeMainWindow((uint)fbWidth, (uint)fbHeight);
            }

            // Drawing should be skipped if the window is minimized.
            if (fbWidth == 0 || fbHeight == 0) continue;

            Draw();
        }

        // Dispose of all resources needed for drawing.
        DisposeResources();

        _glfw.DestroyWindow(_hwnd);
        _glfw.Terminate();
    }

    private static void CreateResources()
    {
        ResourceFactory factory = _graphicsDevice.ResourceFactory;

        VertexPositionColor[] quadVertices =
        {
            new VertexPositionColor(new Vector2(-.75f, .75f), RgbaFloat.Red),
            new VertexPositionColor(new Vector2(.75f, .75f), RgbaFloat.Green),
            new VertexPositionColor(new Vector2(-.75f, -.75f), RgbaFloat.Blue),
            new VertexPositionColor(new Vector2(.75f, -.75f), RgbaFloat.Yellow)
        };
        BufferDescription vbDescription = new BufferDescription(
            4 * VertexPositionColor.SizeInBytes,
            BufferUsage.VertexBuffer);
        _vertexBuffer = factory.CreateBuffer(vbDescription);
        _graphicsDevice.UpdateBuffer(_vertexBuffer, 0, quadVertices);

        ushort[] quadIndices = { 0, 1, 2, 3 };
        BufferDescription ibDescription = new BufferDescription(
            4 * sizeof(ushort),
            BufferUsage.IndexBuffer);
        _indexBuffer = factory.CreateBuffer(ibDescription);
        _graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);

        VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));

        ShaderDescription vertexShaderDesc = new ShaderDescription(
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(VertexCode),
            "main");
        ShaderDescription fragmentShaderDesc = new ShaderDescription(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(FragmentCode),
            "main");

        _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

        // Create pipeline
        GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
        pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
        pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
            depthTestEnabled: true,
            depthWriteEnabled: true,
            comparisonKind: ComparisonKind.LessEqual);
        pipelineDescription.RasterizerState = new RasterizerStateDescription(
            cullMode: FaceCullMode.Back,
            fillMode: PolygonFillMode.Solid,
            frontFace: FrontFace.Clockwise,
            depthClipEnabled: true,
            scissorTestEnabled: false);
        pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
        pipelineDescription.ShaderSet = new ShaderSetDescription(
            vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
            shaders: _shaders);
        pipelineDescription.Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription;

        _pipeline = factory.CreateGraphicsPipeline(pipelineDescription);

        _commandList = factory.CreateCommandList();
    }

    private static void Draw()
    {
        // Begin() must be called before commands can be issued.
        _commandList.Begin();

        // We want to render directly to the output window.
        _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Black);

        // Set all relevant state to draw our quad.
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _commandList.SetPipeline(_pipeline);
        // Issue a Draw command for a single instance with 4 indices.
        _commandList.DrawIndexed(
            indexCount: 4,
            instanceCount: 1,
            indexStart: 0,
            vertexOffset: 0,
            instanceStart: 0);

        // End() must be called before commands can be submitted for execution.
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);

        // Once commands have been submitted, the rendered image can be presented to the application window.
        _graphicsDevice.SwapBuffers();
    }

    private static void DisposeResources()
    {
        _pipeline.Dispose();
        foreach (Shader shader in _shaders)
        {
            shader.Dispose();
        }
        _commandList.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _graphicsDevice.Dispose();
    }

    private static void CreateGraphicsDevice(
        GraphicsBackend backend,
        GraphicsDeviceOptions options,
        int width,
        int height)
    {
        if (backend == GraphicsBackend.OpenGL || backend == GraphicsBackend.OpenGLES)
        {
            _glfw.MakeContextCurrent(_hwnd);

            // The shorter overload is intentional: leaving setSwapchainFramebuffer unset is what makes
            // NeoVeldrid bind the default framebuffer (FBO 0), which is the one GLFW presents from.
            var platformInfo = new OpenGLPlatformInfo(
                openGLContextHandle: (nint)_hwnd,
                getProcAddress: name => (nint)_glfw.GetProcAddress(name),
                makeCurrent: context => _glfw.MakeContextCurrent((WindowHandle*)context),
                getCurrentContext: () => (nint)_glfw.GetCurrentContext(),
                clearCurrentContext: () => _glfw.MakeContextCurrent(null),
                deleteContext: _ => { }, // GLFW handles context cleanup when DestroyWindow is called.
                swapBuffers: () => _glfw.SwapBuffers(_hwnd),
                setSyncToVerticalBlank: sync => _glfw.SwapInterval(sync ? 1 : 0)
            );

            _graphicsDevice = GraphicsDevice.CreateOpenGL(options, platformInfo, (uint)width, (uint)height);
        }
        else
        {
            var swapchainDesc = new SwapchainDescription(
                CreateSwapchainSource(),
                (uint)width,
                (uint)height,
                options.SwapchainDepthFormat,
                options.SyncToVerticalBlank,
                options.SwapchainSrgbFormat
            );

            if (backend == GraphicsBackend.Direct3D11)
            {
                _graphicsDevice = GraphicsDevice.CreateD3D11(options, swapchainDesc);
            }
            else if (backend == GraphicsBackend.Vulkan)
            {
                _graphicsDevice = GraphicsDevice.CreateVulkan(options, swapchainDesc);
            }
            else
            {
                throw new PlatformNotSupportedException($"The {backend} backend is not supported.");
            }
        }
    }

    private static SwapchainSource CreateSwapchainSource()
    {
        var nativeWindow = new GlfwNativeWindow(_glfw, _hwnd);

        // GLFW only fills in the handles for the windowing system it is running on, so the first
        // one with a value is the one to build the SwapchainSource from.
        if (nativeWindow.Win32.HasValue)
        {
            return SwapchainSource.CreateWin32(
                nativeWindow.Win32.Value.Hwnd,
                nativeWindow.Win32.Value.HInstance
            );
        }

        if (nativeWindow.Wayland.HasValue)
        {
            return SwapchainSource.CreateWayland(
                nativeWindow.Wayland.Value.Display,
                nativeWindow.Wayland.Value.Surface
            );
        }

        if (nativeWindow.X11.HasValue)
        {
            return SwapchainSource.CreateXlib(
                (nint)nativeWindow.X11.Value.Display,
                (nint)nativeWindow.X11.Value.Window
            );
        }

        if (nativeWindow.Cocoa.HasValue)
        {
            return SwapchainSource.CreateNSWindow(nativeWindow.Cocoa.Value);
        }

        throw new PlatformNotSupportedException(
            "GLFW did not report a native window handle that NeoVeldrid can create a SwapchainSource from.");
    }
}

struct VertexPositionColor
{
    public const uint SizeInBytes = 24;
    public Vector2 Position;
    public RgbaFloat Color;
    public VertexPositionColor(Vector2 position, RgbaFloat color)
    {
        Position = position;
        Color = color;
    }
}
