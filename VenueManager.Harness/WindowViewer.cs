using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace VenueManager.Harness;

public static class WindowViewer
{
    private const string VertexSource = @"#version 330 core
layout(location = 0) in vec2 position;
layout(location = 1) in vec2 uv;
out vec2 texCoord;
void main()
{
    texCoord = uv;
    gl_Position = vec4(position, 0.0, 1.0);
}";

    private const string FragmentSource = @"#version 330 core
in vec2 texCoord;
out vec4 color;
uniform sampler2D image;
void main()
{
    color = texture(image, texCoord);
}";

    private const double StepSeconds = 0.033;
    private const int SelfTestFrames = 30;
    private const int MinWidth = 400;
    private const int MinHeight = 300;

    public static int Run(PluginHost host, string? shotPath = null)
    {
        var options = WindowOptions.Default with
        {
            Size = new Silk.NET.Maths.Vector2D<int>(host.Width, host.Height),
            Title = "XIV Venue Manager (harness, dev API)",
            WindowBorder = WindowBorder.Resizable,
            VSync = true,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
        };

        var window = Window.Create(options);
        GL? gl = null;
        IInputContext? input = null;
        uint program = 0, vao = 0, vbo = 0, texture = 0;
        ulong lastHash = 0;
        var first = true;
        var sinceStep = 0.0;
        var frames = 0;
        var uploads = 0;
        var uploadMs = 0.0;

        void Fit()
        {
            if (gl is null) return;
            var logical = window.Size;
            if (logical.X < MinWidth || logical.Y < MinHeight) return;
            if (logical.X == host.Width && logical.Y == host.Height) return;
            host.Resize(logical.X, logical.Y);
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)host.Width, (uint)host.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);
            first = true;
        }

        window.Load += () =>
        {
            gl = GL.GetApi(window);
            program = BuildProgram(gl);
            gl.UseProgram(program);
            gl.Uniform1(gl.GetUniformLocation(program, "image"), 0);

            float[] quad =
            {
                -1f, 1f, 0f, 0f,
                -1f, -1f, 0f, 1f,
                1f, 1f, 1f, 0f,
                1f, -1f, 1f, 1f,
            };
            vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);
            vbo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, quad, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            gl.EnableVertexAttribArray(1);
            unsafe
            {
                gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            }

            texture = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)host.Width, (uint)host.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);

            host.Open("Main");
            host.Step(5);

            input = window.CreateInput();
            foreach (var mouse in input.Mice)
            {
                mouse.MouseMove += (_, position) =>
                {
                    var (x, y) = InputMap.ToHost(position.X, position.Y, window.Size.X, window.Size.Y, host.Width, host.Height);
                    host.Imgui.MouseMove(x, y);
                };
                mouse.MouseDown += (_, button) =>
                {
                    if (InputMap.MapButton(button) is { } mapped) host.Imgui.MouseButton(mapped, true);
                };
                mouse.MouseUp += (_, button) =>
                {
                    if (InputMap.MapButton(button) is { } mapped) host.Imgui.MouseButton(mapped, false);
                };
                mouse.Scroll += (_, wheel) => host.Imgui.MouseWheel(wheel.X, wheel.Y);
            }

            foreach (var keyboard in input.Keyboards)
            {
                keyboard.KeyDown += (_, key, _) =>
                {
                    if (InputMap.MapKey(key) is { } mapped) host.Imgui.Key(mapped, true);
                };
                keyboard.KeyUp += (_, key, _) =>
                {
                    if (InputMap.MapKey(key) is { } mapped) host.Imgui.Key(mapped, false);
                };
                keyboard.KeyChar += (_, c) => host.Imgui.Char(c);
            }
        };

        window.Resize += _ => Fit();
        window.FramebufferResize += _ => Fit();

        window.Render += delta =>
        {
            var g = gl!;
            Fit();
            sinceStep += delta;
            if (first || sinceStep >= StepSeconds)
            {
                sinceStep = 0;
                host.Step(1);
                var hash = host.Imgui.DrawDataHash();
                if (first || shotPath is not null || hash != lastHash)
                {
                    var timer = Stopwatch.StartNew();
                    var pixels = host.Imgui.Screenshot();
                    g.BindTexture(TextureTarget.Texture2D, texture);
                    g.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, 0, 0, (uint)host.Width, (uint)host.Height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    uploadMs += timer.Elapsed.TotalMilliseconds;
                    uploads++;
                    lastHash = hash;
                    first = false;
                }
            }

            var size = window.FramebufferSize;
            g.Viewport(0, 0, (uint)size.X, (uint)size.Y);
            g.Clear(ClearBufferMask.ColorBufferBit);
            g.UseProgram(program);
            g.BindVertexArray(vao);
            g.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

            frames++;
            if (shotPath is not null && frames == SelfTestFrames)
            {
                WriteShot(g, size.X, size.Y, shotPath);
                Console.WriteLine($"window shot written: {shotPath}");
                Console.WriteLine($"size {window.Size} framebuffer {window.FramebufferSize} host {host.Width}x{host.Height} png {size.X}x{size.Y}");
                Console.WriteLine($"rasterize+upload: {uploadMs / Math.Max(uploads, 1):F2} ms average over {uploads} uploads");
                window.Close();
            }
        };

        window.Closing += () =>
        {
            input?.Dispose();
            if (gl is null) return;
            gl.DeleteTexture(texture);
            gl.DeleteBuffer(vbo);
            gl.DeleteVertexArray(vao);
            gl.DeleteProgram(program);
        };

        window.Run();
        window.Dispose();
        return 0;
    }

    private static void WriteShot(GL gl, int width, int height, string path)
    {
        var bottomUp = new byte[width * height * 4];
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        gl.ReadPixels<byte>(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, bottomUp.AsSpan());

        var stride = width * 4;
        var topDown = new byte[bottomUp.Length];
        for (var row = 0; row < height; row++)
            System.Buffer.BlockCopy(bottomUp, (height - 1 - row) * stride, topDown, row * stride, stride);

        File.WriteAllBytes(path, Png.Encode(topDown, width, height));
    }

    private static uint BuildProgram(GL gl)
    {
        var vertex = Compile(gl, ShaderType.VertexShader, VertexSource);
        var fragment = Compile(gl, ShaderType.FragmentShader, FragmentSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0) throw new InvalidOperationException($"shader link failed: {gl.GetProgramInfoLog(program)}");
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        return program;
    }

    private static uint Compile(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0) throw new InvalidOperationException($"{type} compile failed: {gl.GetShaderInfoLog(shader)}");
        return shader;
    }
}
