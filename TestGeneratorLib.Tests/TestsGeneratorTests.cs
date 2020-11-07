using NUnit.Framework;
using TestGenerator.Lib;
using System.Linq;

namespace TestGenerator.Tests
{
    public class TestGeneratorTests
    {
        public TestsGenerator generator;

        [SetUp]
        public void Setup()
        {
            generator = new TestsGenerator();
        }

        [Test]
        public void Test1()
        {
            System.Console.WriteLine(generator.Generate().Values.ToList()[0]);
            Assert.Pass();
        }
    }
}