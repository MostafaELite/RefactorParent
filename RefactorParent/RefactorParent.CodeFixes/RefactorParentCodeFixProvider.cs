using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Document = Microsoft.CodeAnalysis.Document;

namespace RefactorParent
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RefactorParentCodeFixProvider)), Shared]
    public class RefactorParentCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("RefactorParent");

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private readonly SymbolDisplayFormat parameterTypeFormat = new SymbolDisplayFormat(
          typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
          miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.FirstOrDefault();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedSolution: c => RefactorParent(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> RefactorParent(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);

            var matchingSymbol = methodSymbol.ContainingType.Interfaces
                  .SelectMany(@interface => @interface.GetMembers())
                  .FirstOrDefault(interfaceMember => interfaceMember.Name == methodSymbol.Name);

            var solution = document.Project.Solution;

            if (!(matchingSymbol is IMethodSymbol matchingMethod))
                return solution;

            var parentMethodRoot = await matchingMethod.DeclaringSyntaxReferences.First().SyntaxTree.GetRootAsync(cancellationToken);
            var parentMethodDeclaration = parentMethodRoot.FindToken(matchingMethod.Locations.First().SourceSpan.Start).Parent
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (parentMethodDeclaration == null)
                return solution;

            var newParameters = GetNewParameters(methodSymbol);
            var newGenericReturnType = GetNewReturnType(methodSymbol);

            var updatedMethod = parentMethodDeclaration
                .RemoveNodes(parentMethodDeclaration.ParameterList.ChildNodes(), SyntaxRemoveOptions.KeepNoTrivia)
                .AddParameterListParameters(newParameters.ToArray())
                .WithReturnType(newGenericReturnType);

            var allSolutionDocs = document.Project.Solution.Projects.SelectMany(project => project.Documents);
            var parentMethodDoc = GetParentDocument(allSolutionDocs, methodSymbol, document, matchingSymbol);
            if (parentMethodDoc is null)
                return solution;

            var parentDocRoot = await parentMethodDoc.GetSyntaxRootAsync(cancellationToken);
            var updatedSyntaxTree = parentDocRoot.ReplaceNode(parentMethodDeclaration, updatedMethod);

            solution = solution.WithDocumentSyntaxRoot(parentMethodDoc.Id, updatedSyntaxTree);
            return solution;

        }

        private static Document GetParentDocument(IEnumerable<Document> solutionDocs, IMethodSymbol methodSymbol, Document childMethodDoc, ISymbol matchingMethodSymbol)
        {
            var praentMethodFile = matchingMethodSymbol.Locations.FirstOrDefault()?.GetLineSpan().Path;
            var parentMethodDoc = solutionDocs.FirstOrDefault(doc => doc.FilePath == praentMethodFile) ??
                solutionDocs.FirstOrDefault(doc => doc.Name == matchingMethodSymbol.ContainingType.Name + ".cs");

            if (parentMethodDoc != null)
                return parentMethodDoc;

            parentMethodDoc = solutionDocs
                .FirstOrDefault(doc =>
                         doc.GetSyntaxRootAsync().Result
                        .DescendantNodes()
                        .OfType<MethodDeclarationSyntax>()
                        .Any(s => s.Identifier.ValueText == methodSymbol.Name) && doc.Id != childMethodDoc.Id);


            return parentMethodDoc;
        }

        private TypeSyntax GetNewReturnType(IMethodSymbol implementingMethod)
        {
            var newReturnType = implementingMethod.ReturnType as INamedTypeSymbol;
            if (!newReturnType.IsGenericType)
                return SyntaxFactory.ParseTypeName(newReturnType.ToDisplayString(parameterTypeFormat));

            var genericArguments = newReturnType.TypeArguments
                .Select(genericArg => SyntaxFactory
                .ParseTypeName(genericArg.ToDisplayString(parameterTypeFormat)))
                .ToArray();

            var newGenericReturnType = SyntaxFactory
                .GenericName(newReturnType.Name)
                .AddTypeArgumentListArguments(genericArguments);
            return newGenericReturnType;
        }

        private IEnumerable<ParameterSyntax> GetNewParameters(IMethodSymbol implementingMethod)
        {
            var newParameters = new List<ParameterSyntax>();
            foreach (var implementationParam in implementingMethod.Parameters)
            {
                var implementationParamType = SyntaxFactory.ParseTypeName(implementationParam.Type.ToDisplayString(parameterTypeFormat));

                var newParamType = implementationParam.Type as INamedTypeSymbol;

                if (newParamType.IsGenericType)
                {
                    var genericArguments = newParamType.TypeArguments
                        .Select(genericArg => SyntaxFactory
                        .ParseTypeName(genericArg.ToDisplayString(parameterTypeFormat)))
                        .ToArray();

                    var newGenericParamType = SyntaxFactory
                        .GenericName(newParamType.Name)
                        .AddTypeArgumentListArguments(genericArguments) as TypeSyntax;

                    var newGenericParam = SyntaxFactory
                                  .Parameter(SyntaxFactory.Identifier(implementationParam.Name))
                                  .WithType(newGenericParamType);

                    newParameters.Add(newGenericParam);

                    continue;
                }

                var newParam = SyntaxFactory
                         .Parameter(SyntaxFactory.Identifier(implementationParam.Name))
                         .WithType(implementationParamType);

                newParameters.Add(newParam);

            }

            return newParameters;
        }
    }
}
