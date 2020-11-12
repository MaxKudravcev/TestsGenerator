namespace TestGenerator.Lib
{
    public class TestUnit
    {
        public string Test { get; }
        public string Name { get; }

        public TestUnit(string test, string name)
        {
            Test = test;
            Name = name;
        }
    }
}
