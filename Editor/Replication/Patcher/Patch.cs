using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Pdb;
using Unity.CompilationPipeline.Common.ILPostProcessing;

public class Patch : ILPostProcessor {
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern void OutputDebugString(string message);

    public override ILPostProcessor GetInstance() => new Patch();

    public override bool WillProcess(ICompiledAssembly compiledAssembly) {
        string name = compiledAssembly.Name;
        if (name.StartsWith("Unity.") || name.StartsWith("UnityEngine.") || name.StartsWith("UnityEditor."))
            return false;

        if (!compiledAssembly.References.Any(r => r.EndsWith("Cube.dll")))
            return false; // No reference to Cube

        OutputDebugString($"{compiledAssembly.Name}: WillProcess");
        return true;
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly) {
        OutputDebugString($"{compiledAssembly.Name}: Start patching...");

        var msgs = new System.Collections.Generic.List<Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage>();

        try {
            using (var stream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData)) {
                var resolver = new DefaultAssemblyResolver();
                foreach (var path in compiledAssembly.References) {
                    var dir = Path.GetDirectoryName(path);
                    if (resolver.GetSearchDirectories().Contains(dir))
                        continue;

                    OutputDebugString($"{compiledAssembly.Name}: Search in {dir}");
                    resolver.AddSearchDirectory(dir);
                }

                var readerParameters = new ReaderParameters() {
                    AssemblyResolver = resolver,
                    SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                    SymbolReaderProvider = new PdbReaderProvider(),
                    ReadSymbols = true,
                    ReadWrite = true,
                };

                OutputDebugString($"{compiledAssembly.Name}: Read assembly...");
                using (var assembly = AssemblyDefinition.ReadAssembly(stream, readerParameters)) {
                    OutputDebugString($"{compiledAssembly.Name}: Patching...");

                    var rpcProcessor = new RpcPostProcessor(assembly.MainModule);
                    var anythingChanged = rpcProcessor.Process(assembly.MainModule);
                    if (!anythingChanged) {
                        OutputDebugString($"{compiledAssembly.Name}: NOTHING CHANGED");
                        return new ILPostProcessResult(compiledAssembly.InMemoryAssembly);
                    }

                    using (var outStream = new MemoryStream()) {
                        using (var outSymbolStream = new MemoryStream()) {
                            var writeParams = new WriterParameters() {
                                SymbolStream = outSymbolStream,
                                SymbolWriterProvider = new PdbWriterProvider(),
                                WriteSymbols = true
                            };
                            assembly.Write(outStream, writeParams);

                            OutputDebugString($"{compiledAssembly.Name}: SUCCESS");

                            return new ILPostProcessResult(new InMemoryAssembly(outStream.ToArray(), outSymbolStream.ToArray()), msgs);
                        }
                    }
                }
            }
        } catch (System.Exception e) {
            var msg = new Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage();
            msg.DiagnosticType = Unity.CompilationPipeline.Common.Diagnostics.DiagnosticType.Error;
            msg.MessageData = e.Message;
            msgs.Add(msg);

            OutputDebugString($"{compiledAssembly.Name}: {e.Message}");
            OutputDebugString($"{compiledAssembly.Name}: {e.StackTrace}");
            return new ILPostProcessResult(null, msgs);
        }
    }
}
