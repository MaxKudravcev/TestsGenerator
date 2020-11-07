﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace TestGenerator.Lib
{

    public class TestsGenerator
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

        public Dictionary<string, string> Generate(string file = test)
        {
            var resDictionary = new Dictionary<string, string>();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(test);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var usings = root.Usings;
            var namespaces = root.DescendantNodes().Where(sn => sn is NamespaceDeclarationSyntax);

            foreach(NamespaceDeclarationSyntax ns in namespaces)
            {
                var classes = ns.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                    resDictionary.Add(@class.Identifier.Text + "Test", GenerateTest(usings, ns, @class));
            }

            return resDictionary;
        }

        private string GenerateTest(IEnumerable<UsingDirectiveSyntax> usings, NamespaceDeclarationSyntax ns, ClassDeclarationSyntax @class)
        {
            usings = usings.Append(CreateUsingDirective("NUnit.Framework"));
            usings = usings.Append(CreateUsingDirective(FindFullNamespace(@class)));
            CompilationUnitSyntax cu = SyntaxFactory.CompilationUnit().AddUsings(usings.ToArray());

            NamespaceDeclarationSyntax testNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.QualifiedName(usings.Last().Name, SyntaxFactory.IdentifierName("Tests")));
            ClassDeclarationSyntax testClass = SyntaxFactory.ClassDeclaration(@class.Identifier.Text + "Tests").AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            testClass = testClass.AddMembers(GenerateMethods(@class));

            cu = cu.AddMembers(testNamespace.AddMembers(testClass));
            return cu.NormalizeWhitespace().ToFullString();
        }

        private UsingDirectiveSyntax CreateUsingDirective(string usingName)
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

        private string FindFullNamespace(ClassDeclarationSyntax @class)
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

        private MethodDeclarationSyntax[] GenerateMethods(ClassDeclarationSyntax @class)
        {
            List<MethodDeclarationSyntax> testMethods = new List<MethodDeclarationSyntax>();
            var methods = @class.ChildNodes().OfType<MethodDeclarationSyntax>();

            foreach(var method in methods)
            {
                MethodDeclarationSyntax testMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName("void"), method.Identifier.Text + "Test");
                testMethod = testMethod.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                testMethod = testMethod.AddAttributeLists(SyntaxFactory.AttributeList().AddAttributes(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Test"))));

                StatementSyntax statement = SyntaxFactory.ParseStatement("Assert.Fail(\"Autogenerated\");");
                testMethod = testMethod.AddBodyStatements(statement);
                testMethods.Add(testMethod);
            }

            return testMethods.ToArray();
        }
    }
}
