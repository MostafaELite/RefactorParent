using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace RefactorParent
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RefactorParentAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "RefactorParent";
        private const string Category = "Design";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var methodSymbol = (context.Symbol as IMethodSymbol);
            var matchingSymbol = methodSymbol
                .ContainingType
                .Interfaces
                .SelectMany(@interface => @interface.GetMembers())
                .FirstOrDefault(interfaceMember => interfaceMember.Name == methodSymbol.Name);

            if (matchingSymbol == null)
                return;

            var parameters = methodSymbol.Parameters;

            var parentMethod = matchingSymbol as IMethodSymbol;
            var parentParameters = parentMethod.Parameters;

            var hasDifferentNumberOfParams = parameters.Length == parentParameters.Length;
            var hasDifferentReturnTypes = parentMethod.ReturnType.Name != methodSymbol.ReturnType.Name;

            var hasMismatch = hasDifferentNumberOfParams || hasDifferentReturnTypes;

            if (!hasMismatch)
            {
                foreach (var implementationParameter in parameters)
                {
                    var parameterMatch = parentParameters.Any(parent =>
                        parent.Name == implementationParameter.Name &&
                        SymbolEqualityComparer.Default.Equals(parent.Type, implementationParameter.Type) &&
                        parent.NullableAnnotation == implementationParameter.NullableAnnotation);

                    if (!parameterMatch)
                    {
                        hasMismatch = true;
                        break;
                    }
                }
            }

            if (!hasMismatch)
                return;

            var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
