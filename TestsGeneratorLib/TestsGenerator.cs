using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace TestGenerator.Lib
{

    public static class TestsGenerator
    {
        const string test =
                          @"using System;
                            using System.Collections;
                            using System.Linq;
                            using System.Text;

                            namespace HelloWorld
                            {
                                class Program
                                {
                                    static void Main(string[] args)
                                    {
                                        Console.WriteLine(""Hello, World!"");
                                    }
                                }
                            }";

        public static TestUnit[] Generate(string file = test)
        {
            List<TestUnit> testUnits = new List<TestUnit>();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(test);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var usings = root.Usings;
            var namespaces = root.DescendantNodes().Where(sn => sn is NamespaceDeclarationSyntax);

            foreach(NamespaceDeclarationSyntax ns in namespaces)
            {
                var classes = ns.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                    testUnits.Add(new TestUnit(GenerateTest(usings, ns, @class), @class.Identifier.Text + "Test"));
            }

            return testUnits.ToArray();
        }

        private static string GenerateTest(IEnumerable<UsingDirectiveSyntax> usings, NamespaceDeclarationSyntax ns, ClassDeclarationSyntax @class)
        {
            usings = usings.Append(CreateUsingDirective("NUnit.Framework"));
            usings = usings.Append(CreateUsingDirective("Moq"));
            usings = usings.Append(CreateUsingDirective(FindFullNamespace(@class)));
            CompilationUnitSyntax cu = SyntaxFactory.CompilationUnit().AddUsings(usings.ToArray());

            NamespaceDeclarationSyntax testNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName($"{usings.Last().Name}.Tests"));
            ClassDeclarationSyntax testClass = SyntaxFactory.ClassDeclaration(@class.Identifier.Text + "Tests").AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            testClass = testClass.AddMembers(GenerateMethods(@class));

            cu = cu.AddMembers(testNamespace.AddMembers(testClass));
            return cu.NormalizeWhitespace().ToFullString();
        }

        private static UsingDirectiveSyntax CreateUsingDirective(string usingName)
        {
            NameSyntax qualifiedName = null;
            foreach (string id in usingName.Split('.'))
            {
                var name = SyntaxFactory.IdentifierName(id);
                if (qualifiedName != null)
                {
                    qualifiedName = SyntaxFactory.QualifiedName(qualifiedName, name);
                }
                else
                {
                    qualifiedName = name;
                }
            }

            return SyntaxFactory.UsingDirective(qualifiedName);
        }

        private static string FindFullNamespace(ClassDeclarationSyntax @class)
        {
            string fullNS = "";

            while (!(@class.Parent is NamespaceDeclarationSyntax))
            {
                fullNS = fullNS.Insert(0, '.' + (@class.Parent as ClassDeclarationSyntax).Identifier.Text);
                @class = @class.Parent as ClassDeclarationSyntax;
            }
            fullNS = ((@class.Parent as NamespaceDeclarationSyntax).Name as IdentifierNameSyntax).Identifier.Text + fullNS;

            return fullNS;
        }

        private static MethodDeclarationSyntax[] GenerateMethods(ClassDeclarationSyntax @class)
        {
            List<MethodDeclarationSyntax> testMethods = new List<MethodDeclarationSyntax>();
            var methods = @class.ChildNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods.Where(method => method.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.PublicKeyword))))
            {
                MethodDeclarationSyntax testMethod = CreateMethodDeclaration(SyntaxKind.PublicKeyword, "void", method.Identifier.Text + "Test", "Test");
                string invokeArgs = "";

                //Arrange
                foreach (ParameterSyntax paramSyn in method.ParameterList.Parameters.Where(param => !param.Identifier.Text.StartsWith('I')))
                {
                    if (!paramSyn.Identifier.Text.StartsWith('I'))
                    {
                        testMethod = testMethod.AddBodyStatements(
                            CreateAssignmentStatement(
                                paramSyn.Type.ToString(),
                                paramSyn.Identifier.Text,
                                false,
                                "default"));
                        invokeArgs += paramSyn.Identifier.Text + ", ";
                    }
                    else invokeArgs += CreateDecoratedName(paramSyn) + ".Object, ";
                }
                if (invokeArgs.Length > 0)
                    invokeArgs = invokeArgs.Remove(invokeArgs.Length - 2);

                //Act + Assert
                if (method.ReturnType == SyntaxFactory.ParseTypeName("void"))
                    testMethod = testMethod.AddBodyStatements(
                        CreateMethodCallStatement(
                            true,
                            "",
                            "",
                            method.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword)) ?
                                @class.Identifier.Text :
                                $"_{@class.Identifier.Text}UnderTest",
                            method.Identifier.Text,
                            invokeArgs),
                        SyntaxFactory.ParseStatement("Assert.Fail(\"Autogenerated\");"));   //Assert
                else
                    testMethod = testMethod.AddBodyStatements(
                        CreateMethodCallStatement(
                            false,
                            method.ReturnType.ToString(),
                            "actual",
                            method.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword)) ?
                                @class.Identifier.Text :
                                $"_{@class.Identifier.Text}UnderTest",
                            method.Identifier.Text,
                            invokeArgs),
                        CreateAssignmentStatement(                                          //Assert
                            method.ReturnType.ToString(),
                            "expected",
                            false,
                            "default"),
                        SyntaxFactory.ParseStatement("Assert.That(actual, Is.EqualTo(expected));"),
                        SyntaxFactory.ParseStatement("Assert.Fail(\"Autogenerated\");"));


                testMethods.Add(testMethod);
            }

            return testMethods.ToArray();
        }

        private static MemberDeclarationSyntax[] GenerateSetUp(ClassDeclarationSyntax @class)
        {
            List<MemberDeclarationSyntax> memberDeclarations = new List<MemberDeclarationSyntax>();
   
            MethodDeclarationSyntax setUpMethod = CreateMethodDeclaration(SyntaxKind.PublicKeyword, "void", "SetUp", "SetUp");
            memberDeclarations.Add(setUpMethod);
            if (@class.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                return memberDeclarations.ToArray();

            memberDeclarations.Add(CreateFieldDeclaration(SyntaxKind.PrivateKeyword, @class.Identifier.Text, $"_{@class.Identifier.Text}UnderTest"));
            ConstructorDeclarationSyntax ctorSyntax = @class.ChildNodes().OfType<ConstructorDeclarationSyntax>()
                                                      .OrderBy(ctorSyn => ctorSyn.ParameterList.Parameters.Count)
                                                      .First();

            foreach(ParameterSyntax paramSyn in ctorSyntax.ParameterList.Parameters.Where(parameter => !parameter.Identifier.Text.StartsWith('I')))
            {
                setUpMethod = setUpMethod.AddBodyStatements(
                    CreateAssignmentStatement(
                        paramSyn.Type.ToString(),
                        paramSyn.Identifier.Text,
                        false,
                        "default"));
            }

            foreach(ParameterSyntax paramSyn in ctorSyntax.ParameterList.Parameters.Where(parameter => parameter.Identifier.Text.StartsWith('I')))
            {
                memberDeclarations.Add(
                    CreateFieldDeclaration(
                        SyntaxKind.PrivateKeyword,
                        $"Mock<{paramSyn.Type.ToString()}>",
                        CreateDecoratedName(paramSyn)));

                setUpMethod = setUpMethod.AddBodyStatements(
                                    CreateAssignmentStatement(
                                        "",
                                        CreateDecoratedName(paramSyn),
                                        true,
                                        $"Mock<{paramSyn.Type.ToString()}>"));
            }

            setUpMethod = setUpMethod.AddBodyStatements(
                CreateAssignmentStatement(
                    "",
                    $"_{@class.Identifier.Text}UnderTest",
                    true,
                    @class.Identifier.Text,
                    string.Join(
                        ", ",
                        ctorSyntax.ParameterList.Parameters
                        .Select(
                            paramSyn => paramSyn.Identifier.Text
                            .StartsWith('I') ?
                            CreateDecoratedName(paramSyn) + ".Object" :
                            paramSyn.Identifier.Text))));

            return memberDeclarations.ToArray();
        }

        private static MethodDeclarationSyntax CreateMethodDeclaration(SyntaxKind accessModifier, string returnType, string methodName, string attributeName = null)
        {
            var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnType), methodName).AddModifiers(SyntaxFactory.Token(accessModifier));
            if (attributeName != null)
                method = method.AddAttributeLists(SyntaxFactory.AttributeList().AddAttributes(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName))));
            return method;
        }

        private static FieldDeclarationSyntax CreateFieldDeclaration(SyntaxKind accessModifier, string fieldType, string fieldName)
        {
            return SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(fieldType))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(fieldName)))))
                .AddModifiers(SyntaxFactory.Token(accessModifier));
        }

        private static string CreateDecoratedName(ParameterSyntax paramSyn)
        {
            return $"_{paramSyn.Identifier.Text}_dependency";
        }

        private static StatementSyntax CreateAssignmentStatement(string type, string var, bool isNew, string assignableVar, string invokeArgs = "")
        {
            return SyntaxFactory.ParseStatement(
                        string.Format(
                            "{0} {1} = {2} {3}{4};",
                            type,
                            var,
                            isNew ? "new" : "",
                            assignableVar,
                            isNew ? string.Format("({0})", invokeArgs) : ""));
        }

        private static StatementSyntax CreateMethodCallStatement(bool isVoid, string type, string var, string obj, string method, string invokeArgs = "")
        {
            return SyntaxFactory.ParseStatement(
                string.Format(
                    isVoid ? "{2}.{3}({4});" : "{0} {1} = {2}.{3}({4});",
                    type,
                    var,
                    obj,
                    method,
                    invokeArgs));
        }
    }
}
