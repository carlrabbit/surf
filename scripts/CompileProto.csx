#r "Nuget: CliWrap, 3.0.0"
using CliWrap;
using CliWrap.Buffered;

string[] _protoArgs = new string[] {
    "-I surf.kestrel/proto",
    "-I surf.kestrel/proto/include",
    "--csharp_out=surf.kestrel/proto.impl",
    "surf.kestrel/proto/hello.proto",
    "surf.kestrel/proto/types.proto",
};

var _res = await Cli.Wrap("../protoc/bin/protoc.exe").WithArguments(string.Join(' ', _protoArgs)).ExecuteBufferedAsync();
Console.WriteLine(_res.StandardOutput);

return 1;
