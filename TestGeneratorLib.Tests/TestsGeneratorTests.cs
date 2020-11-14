using NUnit.Framework;
using TestGenerator.Lib;
using System.Linq;

namespace TestGenerator.Tests
{
    public class TestGeneratorTests
    {
        [Test]
        public void Test1()
        {
            TestUnit[] tests = TestsGenerator.Generate("asd");
            foreach(TestUnit tu in tests)
                System.Console.WriteLine(tu.Test + "\n\n\n");
            Assert.Pass();
        }
    }
}