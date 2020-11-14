using System.Threading.Tasks;

namespace TestGenerator.Tool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new PipelineGenerator().Generate(3, @".\Results", @"..\..\..\ClassForTest.cs");
        }
    }
}