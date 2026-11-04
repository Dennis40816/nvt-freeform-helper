using System.IO.Pipes;
using System.Runtime.CompilerServices;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private pipe slot of <see cref="RuntimeQueryIpcServer"/>.</summary>
internal static class RuntimeQueryIpcServerPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ActivePipe")]
    public static extern NamedPipeServerStream? ActivePipe(RuntimeQueryIpcServer server);
}
