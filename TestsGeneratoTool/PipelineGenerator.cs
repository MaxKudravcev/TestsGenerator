using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TestGenerator.Lib;

namespace TestGenerator.Tool
{
    class PipelineGenerator
    {
        public Task Generate(int maxDegreeOfParallelism, string resultPath, params string[] files)
        {
            var execOptions = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

            var openFile = new TransformBlock<string, string>(async path => await File.ReadAllTextAsync(path), execOptions);
            var generateTests = new TransformManyBlock<string, TestUnit>(file => TestsGenerator.Generate(file), execOptions);
            var writeFile = new ActionBlock<TestUnit>(
                async testUnit => await File.WriteAllTextAsync(
                    Path.Combine(resultPath, testUnit.Name) + ".cs", testUnit.Test),
                execOptions);


            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            openFile.LinkTo(generateTests, linkOptions);
            generateTests.LinkTo(writeFile, linkOptions);


            foreach (string path in files)
                openFile.Post(path);
            openFile.Complete();

            return writeFile.Completion;
        }
    }
}
