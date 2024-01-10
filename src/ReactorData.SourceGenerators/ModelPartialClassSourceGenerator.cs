using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;


namespace ReactorData;

[Generator]
public class ModelPartialClassSourceGenerator : ISourceGenerator
{

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ModelPartialClassSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        SymbolDisplayFormat qualifiedFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
        );

        var receiver = (ModelPartialClassSyntaxReceiver)context.SyntaxReceiver.EnsureNotNull();

        bool HasAttribute(ISymbol symbol, string attributeName)
        {
            // This check assumes that the provided attributeName is either the full name (including namespace)
            // or the metadata name (without "Attribute" suffix), and that you're interested in exact matches only.
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == attributeName
                || attr.AttributeClass?.ToDisplayString() == attributeName);
        }

        foreach (var modelToGenerate in receiver.ModelsToGenerate)
        {
            var semanticModel = context.Compilation.GetSemanticModel(modelToGenerate.SyntaxTree);

            // Get the class symbol from the semantic model
            var classTypeSymbol = semanticModel.GetDeclaredSymbol(modelToGenerate).EnsureNotNull();

            string fullyQualifiedTypeName = classTypeSymbol.ToDisplayString(qualifiedFormat);
            string namespaceName = classTypeSymbol.ContainingNamespace.ToDisplayString();
            string className = classTypeSymbol.Name;


            // Loop through all the properties of the class
            var idProperty = classTypeSymbol.GetMembers()
                .OfType<IPropertySymbol>() // Filter members to only include properties
                .FirstOrDefault(prop =>
                    HasAttribute(prop, "KeyAttribute"));

            idProperty ??= classTypeSymbol.GetMembers()
                    .OfType<IPropertySymbol>() // Filter members to only include properties
                        .FirstOrDefault(prop => prop.Name == "Id");

            idProperty ??= classTypeSymbol.GetMembers()
                    .OfType<IPropertySymbol>() // Filter members to only include properties
                        .FirstOrDefault(prop => prop.Name == "Key");

            if (idProperty == null)
            {
                var diagnosticDescriptor = new DiagnosticDescriptor(
                    id: "REACTOR_DATA_001", // Unique ID for your diagnostic
                    title: $"Model '{fullyQualifiedTypeName}' without key property",
                    messageFormat: "Unable to generate model entity: {0} (Looking for a property named 'Id', 'Key' or with [ModelKey] attribute)", // {0} will be replaced with 'messageArgs'
                    category: "ReactorData Model Attribute",
                    defaultSeverity: DiagnosticSeverity.Warning, // Choose the appropriate severity
                    isEnabledByDefault: true
                );

                // You can now emit the diagnostic with this descriptor and message arguments for the message format.
                var diagnostic = Diagnostic.Create(diagnosticDescriptor, Location.None, fullyQualifiedTypeName);
                context.ReportDiagnostic(diagnostic);

                continue;
            }

            string idPropertyName = idProperty.Name;

            string generatedSource = $$"""
                using System;
                using ReactorData;

                #nullable enable

                namespace {{namespaceName}}
                {
                    partial class {{className}} : IEntity
                    {
                        object? IEntity.GetKey() => {{idPropertyName}} == default ? null : {{idPropertyName}};
                    }
                }
                """;

            context.AddSource($"{fullyQualifiedTypeName}.g.cs", generatedSource);
        }
    }
}

class ModelPartialClassSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> ModelsToGenerate = new();
    public List<PropertyDeclarationSyntax> ModelKeysToGenerate = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax cds)
        {
            var scaffoldAttribute = cds.AttributeLists
                .Where(_ => _.Attributes.Any(attr => attr.Name is IdentifierNameSyntax nameSyntax && 
                    (nameSyntax.Identifier.Text == "Model" || nameSyntax.Identifier.Text == "ModelAttribute")))
                .Select(_ => _.Attributes.First())
                .FirstOrDefault();

            if (scaffoldAttribute != null)
            {
                ModelsToGenerate.Add(cds);
            }
        }
    }
}

public class GeneratorClassItem
{
    public GeneratorClassItem(string @namespace, string className)
    {
        Namespace = @namespace;
        ClassName = className;
    }

    public string Namespace { get; }
    public string ClassName { get; }

    public Dictionary<string, GeneratorFieldItem> FieldItems { get; } = new();
}

public class GeneratorFieldItem
{
    private readonly string? _propMethodName;

    public GeneratorFieldItem(string fieldName, string fieldTypeFullyQualifiedName, FieldAttributeType type, string? propMethodName)
    {
        FieldName = fieldName;
        FieldTypeFullyQualifiedName = fieldTypeFullyQualifiedName;
        Type = type;
        _propMethodName = propMethodName;
    }

    public string FieldName { get; }

    public string FieldTypeFullyQualifiedName { get; }

    public FieldAttributeType Type { get; }

    public string GetPropMethodName()
    {
        if (_propMethodName != null)
        {
            return _propMethodName;
        }

        var fieldName = FieldName.TrimStart('_');
        fieldName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
        return fieldName;
    }
}

public enum FieldAttributeType
{
    Inject,

    Prop
}

