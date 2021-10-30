using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace CK.NUnit.ClassCaseGenerator
{
    [Generator]
    public class ClassCaseGenerator : ISourceGenerator
    {
        public void Initialize( GeneratorInitializationContext context )
        {
            context.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );
        }

        public void Execute( GeneratorExecutionContext context )
        {
            SyntaxReceiver? syntaxReceiver = (SyntaxReceiver?)context.SyntaxReceiver;
            if( syntaxReceiver?.CandidateClasses is null ) throw new NullReferenceException();
            INamedTypeSymbol? attributeType = context.Compilation.GetTypeByMetadataName( "Kuinox.NUnit.ClassCaseAttribute" );
            foreach( ClassDeclarationSyntax candidate in syntaxReceiver.CandidateClasses )
            {
                SemanticModel model = context.Compilation.GetSemanticModel( candidate.SyntaxTree );
                INamedTypeSymbol? symbolInfo = model.GetDeclaredSymbol( candidate );
                if( symbolInfo is null ) continue;
                foreach( AttributeData item in symbolInfo.GetAttributes() )
                {
                    if( item.AttributeClass is null ) continue;
                    if( item.AttributeClass.Equals( attributeType, SymbolEqualityComparer.Default ) )
                    {
                        TypedConstant arg = item.ConstructorArguments.Single();
                        string caseValue = (string)arg.Value!;
                        PropertyDeclarationSyntax classCase = candidate.Members
                            .Where( s => s.Kind() == SyntaxKind.PropertyDeclaration )
                            .Select( s => (PropertyDeclarationSyntax)s )
                            .Where( s => s.Identifier.Text == "ClassCase" )
                            .FirstOrDefault();

                        PropertyDeclarationSyntax newClassCase = classCase
                            .WithExpressionBody( null )
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.LiteralExpression( SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal( caseValue ) )
                                )
                            );
                        string newClassName = candidate.Identifier.Text + "_" + caseValue;
                        ClassDeclarationSyntax updatedClass = candidate.WithMembers(
                            candidate.Members.Remove( classCase ).Add( newClassCase )
                        ).WithIdentifier( SyntaxFactory.Identifier( newClassName ) );
                        
                        context.AddSource( newClassName, updatedClass.ToFullString() );
                        break;
                    }
                }
            }
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax>? CandidateClasses { get; private set; }
            public void OnVisitSyntaxNode( SyntaxNode syntaxNode )
            {
                if( syntaxNode is ClassDeclarationSyntax baseTypeSyntax )
                {
                    if( baseTypeSyntax.AttributeLists.Count == 0 ) return;
                    CandidateClasses ??= new();
                    CandidateClasses.Add( baseTypeSyntax );
                }
            }
        }
    }
}
