using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NeoVeldrid.OpenGL;

internal sealed class OpenGLVersionInfo
{
    public GraphicsApiVersion Version { get; private set; }

    private OpenGLVersionInfo()
    {
        Version = GraphicsApiVersion.Unknown;
    }
    public OpenGLVersionInfo(GraphicsBackend backend)
    {
        Version = GraphicsApiVersion.Unknown;

        if (backend == GraphicsBackend.OpenGL || backend == GraphicsBackend.OpenGLES)
            GetVersion(backend);
    }

    private void GetVersion(GraphicsBackend backend)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string version = (backend == GraphicsBackend.OpenGL)
                ? Windows.GetOpenGLVersionString()
                : Windows.GetOpenGLESVersionString();

            Version = ParseGLVersionString(version);
        }
    }

    private GraphicsApiVersion ParseGLVersionString(string version)
    {
        GraphicsApiVersion result = GraphicsApiVersion.Unknown;

        do
        {
            if (string.IsNullOrWhiteSpace(version))
                break;

            // OpenGL / OpenGL ES version strings can have a bunch of boilerplate
            // like vendor information, so this Regex strips it out and just gives
            // us the relevant numbers.
            Regex regex = new Regex(@"(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?");
            Match match = regex.Match(version);
            if (!match.Success)
                break;

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);

            // Patch is not guaranteed to be present in OpenGL versions strings.
            int patch = 0;
            if (match.Groups["patch"].Success)
                patch = int.Parse(match.Groups["patch"].Value);

            result = new GraphicsApiVersion(major, minor, 0, patch);

            break;
        }
        while (true);

        return result;
    }

    private static class Windows
    {
        private delegate IntPtr wglCreateContextAttribsARB(IntPtr hDC, IntPtr hShareContext, int[] attribList);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);
        [DllImport("gdi32.dll")]
        private static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("opengl32.dll")]
        private static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport("opengl32.dll")]
        private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        [DllImport("opengl32.dll")]
        private static extern bool wglDeleteContext(IntPtr hglrc);

        [DllImport("opengl32.dll")]
        private static extern IntPtr glGetString(uint name);

        [DllImport("opengl32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr wglGetProcAddress(string lpszProc);

        public static string GetOpenGLVersionString()
        {
            IntPtr hwnd = IntPtr.Zero;
            IntPtr hdc = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;

            string result = string.Empty;
            do
            {
                hwnd = CreateWindowEx(0, "STATIC", "", 0, 0, 0, 1, 1,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                    break;

                hdc = GetDC(hwnd);
                if (hdc == IntPtr.Zero)
                    break;

                PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR
                {
                    nSize = (ushort)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR)),
                    nVersion = 1,
                    dwFlags = 0x00000004 |              // PFD_DRAW_TO_WINDOW
                              0x00000020,               // PFD_SUPPORT_OPENGL
                    iPixelType = 0,                     // PFD_TYPE_RGBA
                    cColorBits = 32,
                };

                int pixelFormat = ChoosePixelFormat(hdc, ref pfd);
                if (pixelFormat == 0 || !SetPixelFormat(hdc, pixelFormat, ref pfd))
                    break;

                context = wglCreateContext(hdc);
                if (context == IntPtr.Zero)
                    break;
                wglMakeCurrent(hdc, context);

                IntPtr versionPtr = glGetString(0x1F02); // GL_VERSION
                if (versionPtr == IntPtr.Zero)
                    break;
                result = Marshal.PtrToStringAnsi(versionPtr);

                break;
            }
            while (true);

            if (context != IntPtr.Zero)
            {
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                wglDeleteContext(context);
            }
            if (hdc != IntPtr.Zero)
                ReleaseDC(hwnd, hdc);
            if (hwnd != IntPtr.Zero)
                DestroyWindow(hwnd);

            return result;
        }

        public static string GetOpenGLESVersionString()
        {
            IntPtr hwnd = IntPtr.Zero;
            IntPtr hdc = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;

            string result = string.Empty;
            do
            {
                hwnd = CreateWindowEx(0, "STATIC", "", 0, 0, 0, 1, 1,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                    break;

                hdc = GetDC(hwnd);
                if (hdc == IntPtr.Zero)
                    break;

                PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR
                {
                    nSize = (ushort)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR)),
                    nVersion = 1,
                    dwFlags = 0x00000004 |              // PFD_DRAW_TO_WINDOW
                              0x00000020,               // PFD_SUPPORT_OPENGL
                    iPixelType = 0,                     // PFD_TYPE_RGBA
                    cColorBits = 32,
                };

                int pixelFormat = ChoosePixelFormat(hdc, ref pfd);
                if (pixelFormat == 0 || !SetPixelFormat(hdc, pixelFormat, ref pfd))
                    break;

                context = wglCreateContext(hdc);
                if (context == IntPtr.Zero)
                    break;
                wglMakeCurrent(hdc, context);

                // We needed a regular OpenGL context for bootstrapping, now we get into
                // the actual ES stuff.
                IntPtr pCreateContextAttribs = wglGetProcAddress("wglCreateContextAttribsARB");
                if (pCreateContextAttribs == IntPtr.Zero)
                    break;
                var createContextAttribs = Marshal.GetDelegateForFunctionPointer<wglCreateContextAttribsARB>(
                    pCreateContextAttribs);

                int[] attribs = new int[]
                {
                    0x2091, 2,      // WGL_CONTEXT_MAJOR_VERSION_ARB
                    0x2092, 0,      // WGL_CONTEXT_MINOR_VERSION_ARB
                    0x9126,         // WGL_CONTEXT_PROFILE_MASK_ARB
                    0x00000004,     // WGL_CONTEXT_ES2_PROFILE_BIT_EXT
                    0
                };

                IntPtr esContext = createContextAttribs(hdc, IntPtr.Zero, attribs);
                if (esContext == IntPtr.Zero)
                    break;
                wglMakeCurrent(hdc, esContext);

                IntPtr versionPtr = glGetString(0x1F02); // GL_VERSION
                if (versionPtr == IntPtr.Zero)
                    break;
                result = Marshal.PtrToStringAnsi(versionPtr);

                // Safely delete the OpenGL ES context.
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                wglDeleteContext(esContext);

                break;
            }
            while (true);

            if (context != IntPtr.Zero)
            {
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                wglDeleteContext(context);
            }
            if (hdc != IntPtr.Zero)
                ReleaseDC(hwnd, hdc);
            if (hwnd != IntPtr.Zero)
                DestroyWindow(hwnd);

            return result;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public byte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }
    }
}
